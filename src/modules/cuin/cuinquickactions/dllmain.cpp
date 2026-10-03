// [fork-feature] Cuin Quick Actions —— PowerToys Cuin 自有模块的 C++ 壳。
// 职责：按 runner 生命周期启停 CuinQuickActions.UI.exe（WinUI3 面板进程），
// 并把集中式热键转发为命名事件。业务逻辑全部在 C# 侧，本文件保持最小。
// 架构说明见 docs/CUSTOM_MODULE_GUIDE.md。

#include "pch.h"
#include <atlbase.h>
#include <atomic>
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/elevation.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>
#include <filesystem>
#include <interface/powertoy_module_interface.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/,
                      DWORD ul_reason_for_call,
                      LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    }
    return TRUE;
}

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ActivationShortcut";
}

// [fork-feature] 模块名 = Settings enabled key = 数据目录名（%LOCALAPPDATA%\PowerToysCuin\CuinQuickActions）。
const static wchar_t* MODULE_NAME = L"CuinQuickActions";
const static wchar_t* MODULE_DESC = L"A quick actions panel for common system tasks.";

// [fork-feature] Cuin Quick Actions 模块：只负责启停 UI 进程与转发热键事件。
class CuinQuickActions : public PowertoyModuleIface
{
private:
    // 模块启停状态。
    bool m_enabled = false;

    Hotkey m_hotkey;

    HANDLE m_hProcess = 0;
    DWORD m_processPid = 0;

    HANDLE m_hInvokeEvent;
    HANDLE m_hTerminateEvent;

    // 读取模块 settings.json（损坏时保持默认值，绝不让 runner 崩溃）。
    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(CuinQuickActions::get_name());

            parse_settings(settings);
        }
        catch (std::exception&)
        {
            // settings 文件缺失或损坏：沿用默认热键（Ctrl+Alt+Q）。
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                parse_hotkey(jsonHotkeyObject);
            }
            catch (...)
            {
                Logger::error("Failed to initialize Cuin Quick Actions hotkey settings");
                set_default_key_settings();
            }
        }
        else
        {
            set_default_key_settings();
        }
    }

    void set_default_key_settings()
    {
        Logger::info("Cuin Quick Actions is going to use default key settings (Ctrl+Alt+Q)");
        m_hotkey.win = false;
        m_hotkey.alt = true;
        m_hotkey.shift = false;
        m_hotkey.ctrl = true;
        m_hotkey.key = 'Q';
    }

    void parse_hotkey(winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject)
    {
        try
        {
            m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize Cuin Quick Actions start shortcut");
        }

        if (!m_hotkey.key)
        {
            set_default_key_settings();
        }
    }

    bool is_panel_running()
    {
        if (m_hProcess == 0)
        {
            return false;
        }
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    // 启动 UI 进程。Quick Actions 面板全程不需要管理员权限：
    // runner 提权时也强制以普通权限运行（最小权限原则）。
    void launch_process()
    {
        Logger::trace(L"Starting CuinQuickActions.UI process");

        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        if (is_process_elevated(false))
        {
            Logger::trace("Starting Cuin Quick Actions non elevated from elevated process");
            const auto modulePath = get_module_folderpath();
            std::wstring runExecutablePath = modulePath;
            runExecutablePath += L"\\WinUI3Apps\\PowerToys.CuinQuickActions.UI.exe";
            std::optional<ProcessInfo> processStartedInfo = RunNonElevatedFailsafe(
                runExecutablePath,
                executable_args,
                modulePath,
                PROCESS_QUERY_INFORMATION | SYNCHRONIZE | PROCESS_TERMINATE);
            if (processStartedInfo.has_value())
            {
                m_processPid = processStartedInfo.value().processID;
                m_hProcess = processStartedInfo.value().processHandle.release();
            }
            else
            {
                Logger::error(L"CuinQuickActions.UI failed to start not elevated.");
            }
        }
        else
        {
            SHELLEXECUTEINFOW sei{ sizeof(sei) };

            sei.fMask = { SEE_MASK_NOCLOSEPROCESS };
            sei.lpVerb = L"open";
            sei.lpFile = L"WinUI3Apps\\PowerToys.CuinQuickActions.UI.exe";
            sei.nShow = SW_SHOWNORMAL;
            sei.lpParameters = executable_args.data();

            if (ShellExecuteExW(&sei))
            {
                Logger::trace("Successfully started the CuinQuickActions.UI process");
            }
            else
            {
                Logger::error(L"CuinQuickActions.UI failed to start. {}", get_last_error_or_default(GetLastError()));
            }

            m_hProcess = sei.hProcess;
            m_processPid = GetProcessId(m_hProcess);
        }
    }

public:
    CuinQuickActions()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", "CuinQuickActions");
        init_settings();

        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::CUIN_QUICK_ACTIONS_SHOW_EVENT);
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::CUIN_QUICK_ACTIONS_TERMINATE_EVENT);
    };

    ~CuinQuickActions()
    {
        m_enabled = false;
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // 不合法的 JSON：忽略本次更新，保留当前配置。
        }
    }

    // Enable the powertoy
    virtual void enable() override
    {
        Logger::trace("CuinQuickActions::enable()");
        ResetEvent(m_hInvokeEvent);
        if (!is_panel_running())
        {
            launch_process();
        }
        m_enabled = true;
    }

    // Disable the powertoy：通知面板退出，超时才强杀。禁用态零进程。
    virtual void disable() override
    {
        Logger::trace("CuinQuickActions::disable()");
        if (m_enabled)
        {
            ResetEvent(m_hInvokeEvent);
            SetEvent(m_hTerminateEvent);

            HANDLE hProcess = OpenProcess(SYNCHRONIZE | PROCESS_TERMINATE, FALSE, m_processPid);
            if (hProcess)
            {
                if (WaitForSingleObject(hProcess, 1500) == WAIT_TIMEOUT)
                {
                    auto result = TerminateProcess(hProcess, 1);
                    if (result == 0)
                    {
                        int error = GetLastError();
                        Logger::trace("Couldn't terminate the CuinQuickActions.UI process. Last error: {}", error);
                    }
                }

                CloseHandle(hProcess);
            }

            CloseHandle(m_hProcess);
            m_hProcess = 0;
            m_processPid = 0;
        }

        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // [fork-feature] settings.json 尚无本模块键时的 runner 侧默认值。必须与 Settings 侧
    // EnabledModules.CuinQuickActions 的 C# 默认值（defaulting to off）一致，否则会出现
    // "开关显示关闭但模块实际在运行"的状态漂移（preview.3 升级链真机验证发现）。
    bool is_enabled_by_default() const override
    {
        return false;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_hotkey;
            }

            return 1;
        }
        else
        {
            return 0;
        }
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Cuin Quick Actions hotkey pressed");

            if (!is_panel_running())
            {
                launch_process();
            }

            SetEvent(m_hInvokeEvent);
            return true;
        }

        return false;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CuinQuickActions();
}
