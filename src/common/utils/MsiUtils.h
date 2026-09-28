#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>
#include <pathcch.h>
#include <Msi.h>

#include <optional>
#include <string>

namespace // Strings in this namespace should not be localized
{
    // [fork-identity] fork 专属 MSI 身份（与 installer/PowerToysSetupVNext/Common.wxi 保持一致），
    // 用于探测 fork 自身旧版安装路径与更新白名单；官方码见 docs/IDENTITY_MAP.md。
    // 组件 GUID 对应本 fork MSI 的 powertoys_exe 组件（installer/PowerToysSetupVNext/Core.wxs）。
    const inline wchar_t POWER_TOYS_UPGRADE_CODE[] = L"{78975C14-0AA0-41A7-99C2-55F44200A919}";
    const inline wchar_t POWER_TOYS_UPGRADE_CODE_USER[] = L"{DA33EB25-63A5-4E9A-8B04-D8AE81BB888D}";
    const inline wchar_t POWERTOYS_EXE_COMPONENT[] = L"{30261594-41A6-4509-AD09-FBC4E692F441}";

    // [fork-identity] 官方 Microsoft PowerToys 的升级链锚点，仅用于"并存检测提示"，
    // 绝不用于修改、覆盖或卸载官方产品。与 docs/IDENTITY_MAP.md §1 的检测锚一致。
    const inline wchar_t OFFICIAL_POWER_TOYS_UPGRADE_CODE[] = L"{42B84BF7-5FBF-473B-9C8B-049DC16F7708}";
    const inline wchar_t OFFICIAL_POWER_TOYS_UPGRADE_CODE_USER[] = L"{D8B559DB-4C98-487A-A33F-50A8EEE42726}";
}

// 按 UpgradeCode 枚举并校验产品注册状态（存在即返回 true）。
inline bool is_msi_upgrade_code_present(const wchar_t* upgrade_code)
{
    constexpr size_t guid_length = 39;
    wchar_t product_ID[guid_length];
    if (ERROR_SUCCESS != MsiEnumRelatedProductsW(upgrade_code, 0, 0, product_ID))
    {
        return false;
    }
    return INSTALLSTATE_DEFAULT == MsiQueryProductStateW(product_ID);
}

// [fork-identity] 官方 PowerToys 是否已在本机安装（任一 scope）。供 runner 启动时给出
// "不支持同时安装" 提示；检测结果不触发任何自动化动作。
inline bool IsOfficialPowerToysInstalled()
{
    return is_msi_upgrade_code_present(OFFICIAL_POWER_TOYS_UPGRADE_CODE) ||
           is_msi_upgrade_code_present(OFFICIAL_POWER_TOYS_UPGRADE_CODE_USER);
}

inline std::optional<std::wstring> GetMsiPackageInstalledPath(bool perUser)
{
    constexpr size_t guid_length = 39;
    wchar_t product_ID[guid_length];
    std::wstring upgradeCode = (perUser ? POWER_TOYS_UPGRADE_CODE_USER : POWER_TOYS_UPGRADE_CODE);
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(upgradeCode.c_str(), 0, 0, product_ID); !found)
    {
        return std::nullopt;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(product_ID); !installed)
    {
        return std::nullopt;
    }

    DWORD buf_size = MAX_PATH;
    wchar_t buf[MAX_PATH];
    if (ERROR_SUCCESS == MsiGetProductInfoW(product_ID, INSTALLPROPERTY_INSTALLLOCATION, buf, &buf_size) && buf_size)
    {
        return buf;
    }

    DWORD package_path_size = 0;

    if (ERROR_SUCCESS != MsiGetProductInfoW(product_ID, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size))
    {
        return std::nullopt;
    }
    std::wstring package_path(++package_path_size, L'\0');

    if (ERROR_SUCCESS != MsiGetProductInfoW(product_ID, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size))
    {
        return std::nullopt;
    }
    package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    wchar_t path[MAX_PATH];
    DWORD path_size = MAX_PATH;
    MsiGetComponentPathW(product_ID, POWERTOYS_EXE_COMPONENT, path, &path_size);
    if (!path_size)
    {
        return std::nullopt;
    }
    PathCchRemoveFileSpec(path, path_size);
    return path;
}

inline std::wstring GetMsiPackagePath()
{
    std::wstring package_path;
    wchar_t GUID_product_string[39];
    if (const bool found = ERROR_SUCCESS == MsiEnumRelatedProductsW(POWER_TOYS_UPGRADE_CODE, 0, 0, GUID_product_string); !found)
    {
        return package_path;
    }

    if (const bool installed = INSTALLSTATE_DEFAULT == MsiQueryProductStateW(GUID_product_string); !installed)
    {
        return package_path;
    }

    DWORD package_path_size = 0;

    if (const bool has_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, nullptr, &package_path_size); !has_package_path)
    {
        return package_path;
    }

    package_path = std::wstring(++package_path_size, L'\0');
    if (const bool got_package_path = ERROR_SUCCESS == MsiGetProductInfoW(GUID_product_string, INSTALLPROPERTY_LOCALPACKAGE, package_path.data(), &package_path_size); !got_package_path)
    {
        package_path = {};
        return package_path;
    }

    package_path.resize(size(package_path) - 1); // trim additional \0 which we got from MsiGetProductInfoW

    return package_path;
}
