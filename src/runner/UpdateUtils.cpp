#include "pch.h"

#include "Generated Files/resource.h"

#include "ActionRunnerUtils.h"
#include "general_settings.h"
#include "trace.h"
#include "tray_icon.h"
#include "UpdateUtils.h"

#include <common/utils/gpo.h>
#include <common/logger/logger.h>
#include <common/notifications/notifications.h>
#include <common/updating/installer.h>
#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/utils/HttpClient.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>
#include <common/version/version.h>

namespace
{
    constexpr int64_t UPDATE_CHECK_INTERVAL_MINUTES = 60 * 24;
    constexpr int64_t UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES = 60 * 2;

    // How many minor versions to suspend the toast notification (example: installed=0.60.0, suspend=2, next notification=0.63.*)
    // Attention: When changing this value please update the ADML file to.
    const int UPDATE_NOTIFICATION_TOAST_SUSPEND_MINOR_VERSION_COUNT = 2;

    // The per-user "include prerelease updates" opt-in, additionally gated by the DisablePreviewUpdates
    // group policy: when that policy is Enabled, preview (prerelease) updates are forced off regardless
    // of the user's setting. Stable updates are unaffected.
    bool effective_include_prerelease_updates()
    {
        if (powertoys_gpo::getDisablePreviewUpdatesValue() == powertoys_gpo::gpo_rule_configured_enabled)
        {
            return false;
        }
        return get_general_settings().includePrereleaseUpdates;
    }
}
using namespace notifications;
using namespace updating;

std::wstring AvailableVersionToWstring(const new_version_download_info& info)
{
    auto result = info.version.toWstring();
    if (info.is_prerelease)
    {
        result += L"-preview";
    }

    return result;
}

std::wstring CurrentVersionToNextVersion(const new_version_download_info& info)
{
    auto result = VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
    result += L" \u2192 "; // Right arrow
    result += AvailableVersionToWstring(info);
    return result;
}

std::wstring UpdateAvailableMessage(const new_version_download_info& info)
{
    return info.is_prerelease ?
               GET_RESOURCE_STRING(IDS_GITHUB_NEW_PREVIEW_VERSION_AVAILABLE) :
               GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_AVAILABLE);
}

void ShowNewVersionAvailable(const new_version_download_info& info)
{
    remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

    toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
    std::wstring contents = UpdateAvailableMessage(info);
    contents += L'\n';
    contents += CurrentVersionToNextVersion(info);

    show_toast_with_activations(std::move(contents),
                                GET_RESOURCE_STRING(IDS_TOAST_TITLE),
                                {},
                                { link_button{ GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_NOW),
                                               L"powertoys://update_now/" },
                                  link_button{ GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_MORE_INFO),
                                               L"powertoys://open_overview/" } },
                                std::move(toast_params),
                                L"powertoys://open_overview/");
}

void ShowOpenSettingsForUpdate(const new_version_download_info& info)
{
    remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

    toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };

    std::vector<action_t> actions = {
        link_button{ GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_MORE_INFO),
                     L"powertoys://open_overview/" },
    };
    auto contents = UpdateAvailableMessage(info);
    contents += L'\n';
    contents += AvailableVersionToWstring(info);
    show_toast_with_activations(std::move(contents),
                                GET_RESOURCE_STRING(IDS_TOAST_TITLE),
                                {},
                                std::move(actions),
                                std::move(toast_params),
                                L"powertoys://open_overview/");
}

// [fork-identity] Binary Preview 阶段禁用自动更新安装：fork 尚无自己的代码签名与更新信任链，
// verify_installer_trust 只信任微软签名（fork 包会被安全拒绝）。因此：
//  - 后台 PeriodicUpdateWorker 整体短路（不检查、不下载、不执行）；
//  - 所有"立即更新/检查更新"入口（toast 按钮、Settings 按钮）改为打开 fork 的 GitHub Releases 页，
//    由用户手动下载并自行校验 SHA256（见 docs/RELEASE_CHECKLIST.md）。
// 拥有 fork 签名信任链后，将本常量改为 true 即恢复上游更新流水线（docs/CODE_SIGNING.md）。
constexpr bool FORK_AUTO_UPDATE_INSTALL_ENABLED = false;
const wchar_t FORK_RELEASES_PAGE_URL[] = L"https://github.com/yangxijia111/PowerToys/releases";

void OpenForkReleasesPage()
{
    ShellExecuteW(nullptr, L"open", FORK_RELEASES_PAGE_URL, nullptr, nullptr, SW_SHOWNORMAL);
}

SHELLEXECUTEINFOW LaunchPowerToysUpdate(const wchar_t* cmdline)
{
    if constexpr (!FORK_AUTO_UPDATE_INSTALL_ENABLED)
    {
        // Binary Preview：不下载、不执行任何更新包，改为引导用户到 Releases 页手动更新。
        Logger::info(L"Auto update install is disabled in Binary Preview; opening releases page instead.");
        OpenForkReleasesPage();
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        return sei;
    }

    std::wstring powertoysUpdaterPath;
    powertoysUpdaterPath = get_module_folderpath();

    powertoysUpdaterPath += L"\\PowerToys.Update.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS };
    sei.lpFile = powertoysUpdaterPath.c_str();
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = cmdline;
    ShellExecuteExW(&sei);
    return sei;
}

bool IsMeteredConnection()
{
    using namespace winrt::Windows::Networking::Connectivity;
    ConnectionProfile internetConnectionProfile = NetworkInformation::GetInternetConnectionProfile();
    if (!internetConnectionProfile)
    {
        return false;
    }

    if (internetConnectionProfile.IsWwanConnectionProfile())
    {
        return true;
    }

    ConnectionCost connectionCost = internetConnectionProfile.GetConnectionCost();
    if (connectionCost.Roaming()
        || connectionCost.OverDataLimit()
        || connectionCost.NetworkCostType() == NetworkCostType::Fixed
        || connectionCost.NetworkCostType() == NetworkCostType::Variable)
    {
        return true;
    }

    return false;
}

void ProcessNewVersionInfo(const github_version_info& version_info,
                           UpdateState& state,
                           const bool download_update,
                           bool show_notifications)
{
    state.githubUpdateLastCheckedDate.emplace(timeutil::now());
    if (std::holds_alternative<version_up_to_date>(version_info))
    {
        state.state = UpdateState::upToDate;
        state.releasePageUrl = {};
        state.downloadedInstallerFilename = {};
        state.isPrerelease = false;
        Logger::trace(L"Version is up to date");
        dispatch_run_on_main_ui_thread([](PVOID) { set_tray_icon_update_available(false); }, nullptr);
        return;
    }
    const auto new_version_info = std::get<new_version_download_info>(version_info);
    state.releasePageUrl = new_version_info.release_page_uri.ToString().c_str();
    state.isPrerelease = new_version_info.is_prerelease;
    Logger::trace(L"Discovered new version {}", new_version_info.version.toWstring());

    const bool already_downloaded = state.state == UpdateState::readyToInstall && state.downloadedInstallerFilename == new_version_info.installer_filename;
    if (already_downloaded)
    {
        Logger::trace(L"New version is already downloaded");
        return;
    }

    // Check toast notification GPOs and settings. (We check only if notifications are allowed. This is the case if we are triggered by the periodic check.)
    // Disable notification GPO or setting
    bool disable_notification_setting = get_general_settings().showNewUpdatesToastNotification == false;
    if (show_notifications && (disable_notification_setting || powertoys_gpo::getDisableNewUpdateToastValue() == powertoys_gpo::gpo_rule_configured_enabled))
    {
        Logger::info(L"There is a new update available or ready to install. But the toast notification is disabled by setting or GPO.");
        show_notifications = false;
    }
    // Suspend notification GPO
    else if (show_notifications && powertoys_gpo::getSuspendNewUpdateToastValue() == powertoys_gpo::gpo_rule_configured_enabled)
    {
        Logger::info(L"GPO to suspend new update toast notification is enabled.");
        if (new_version_info.version.major <= VERSION_MAJOR && new_version_info.version.minor - VERSION_MINOR <= UPDATE_NOTIFICATION_TOAST_SUSPEND_MINOR_VERSION_COUNT)
        {
            Logger::info(L"The difference between the installed version and the newer version is within the allowed period. The toast notification is not shown.");
            show_notifications = false;
        }
        else
        {
            Logger::info(L"The installed version is older than allowed for suspending the toast notification. The toast notification is shown.");
        }
    }

    if (download_update)
    {
        Logger::trace(L"Downloading installer for a new version");

        // Cleanup old updates before downloading the latest
        updating::cleanup_updates();

        auto downloaded_installer = std::move(download_new_version_async(new_version_info)).get();
        if (downloaded_installer)
        {
            state.state = UpdateState::readyToInstall;
            state.downloadedInstallerFilename = new_version_info.installer_filename;
            Trace::UpdateDownloadCompleted(true, new_version_info.version.toWstring());
            dispatch_run_on_main_ui_thread([](PVOID) { set_tray_icon_update_available(true); }, nullptr);
            if (show_notifications)
            {
                ShowNewVersionAvailable(new_version_info);
            }
        }
        else
        {
            state.state = UpdateState::errorDownloading;
            state.downloadedInstallerFilename = {};
            Trace::UpdateDownloadCompleted(false, new_version_info.version.toWstring());
            Logger::error("Couldn't download new installer");
        }
    }
    else
    {
        Logger::trace(L"New version is ready to download, showing notification");
        state.state = UpdateState::readyToDownload;
        state.downloadedInstallerFilename = {};
        dispatch_run_on_main_ui_thread([](PVOID) { set_tray_icon_update_available(true); }, nullptr);
        if (show_notifications)
        {
            ShowOpenSettingsForUpdate(new_version_info);
        }
    }
}

void PeriodicUpdateWorker()
{
    if constexpr (!FORK_AUTO_UPDATE_INSTALL_ENABLED)
    {
        // [fork-identity] Binary Preview：后台自动更新 worker 不启动。
        return;
    }
    for (;;)
    {
        auto state = UpdateState::read();
        int64_t sleep_minutes_till_next_update = UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES;
        if (state.githubUpdateLastCheckedDate.has_value())
        {
            int64_t last_checked_minutes_ago = timeutil::diff::in_minutes(timeutil::now(), *state.githubUpdateLastCheckedDate);
            if (last_checked_minutes_ago < 0)
            {
                last_checked_minutes_ago = UPDATE_CHECK_INTERVAL_MINUTES;
            }
            sleep_minutes_till_next_update = max(0, UPDATE_CHECK_INTERVAL_MINUTES - last_checked_minutes_ago);
        }

        std::this_thread::sleep_for(std::chrono::minutes{ sleep_minutes_till_next_update });

        // Auto download setting.
        bool download_update = !IsMeteredConnection() && get_general_settings().downloadUpdatesAutomatically;
        if (powertoys_gpo::getDisableAutomaticUpdateDownloadValue() == powertoys_gpo::gpo_rule_configured_enabled)
        {
            Logger::info(L"Automatic download of updates is disabled by GPO.");
            download_update = false;
        }

        bool version_info_obtained = false;
        try
        {
            const auto new_version_info = std::move(get_github_version_info_async(effective_include_prerelease_updates())).get();
            if (new_version_info.has_value())
            {
                version_info_obtained = true;
                bool updateAvailable = std::holds_alternative<new_version_download_info>(*new_version_info);
                std::wstring fromVersion = get_product_version();
                std::wstring toVersion = updateAvailable ? std::get<new_version_download_info>(*new_version_info).version.toWstring() : L"";
                Trace::UpdateCheckCompleted(true, updateAvailable, fromVersion, toVersion);
                ProcessNewVersionInfo(*new_version_info, state, download_update, true);
            }
            else
            {
                Trace::UpdateCheckCompleted(false, false, get_product_version(), L"");
                Logger::error(L"Couldn't obtain version info from github: {}", new_version_info.error());
            }
        }
        catch (...)
        {
            Logger::error("periodic_update_worker: error while processing version info");
        }

        if (version_info_obtained)
        {
            UpdateState::store([&](UpdateState& v) {
                v = std::move(state);
            });
        }
        else
        {
            std::this_thread::sleep_for(std::chrono::minutes{ UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES });
        }
    }
}

void CheckForUpdatesCallback()
{
    Logger::trace(L"Check for updates callback invoked");
    if constexpr (!FORK_AUTO_UPDATE_INSTALL_ENABLED)
    {
        // [fork-identity] Binary Preview：手动检查更新直接打开 Releases 页（版本检查与下载/执行分离）。
        OpenForkReleasesPage();
        return;
    }
    auto state = UpdateState::read();
    try
    {
        auto new_version_info = std::move(get_github_version_info_async(effective_include_prerelease_updates())).get();
        if (!new_version_info)
        {
            // We couldn't get a new version from github for some reason, log error
            state.state = UpdateState::networkError;
            Trace::UpdateCheckCompleted(false, false, get_product_version(), L"");
            Logger::error(L"Couldn't obtain version info from github: {}", new_version_info.error());
        }
        else
        {
            // Auto download setting
            bool download_update = !IsMeteredConnection() && get_general_settings().downloadUpdatesAutomatically;
            if (powertoys_gpo::getDisableAutomaticUpdateDownloadValue() == powertoys_gpo::gpo_rule_configured_enabled)
            {
                Logger::info(L"Automatic download of updates is disabled by GPO.");
                download_update = false;
            }

            bool updateAvailable = std::holds_alternative<new_version_download_info>(*new_version_info);
            std::wstring fromVersion = get_product_version();
            std::wstring toVersion = updateAvailable ? std::get<new_version_download_info>(*new_version_info).version.toWstring() : L"";
            Trace::UpdateCheckCompleted(true, updateAvailable, fromVersion, toVersion);
            ProcessNewVersionInfo(*new_version_info, state, download_update, false);
        }

        UpdateState::store([&](UpdateState& v) {
            v = std::move(state);
        });
    }
    catch (...)
    {
        Logger::error("CheckForUpdatesCallback: error while processing version info");
    }
}
