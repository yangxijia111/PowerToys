#pragma once

#define STRINGIZE2(s) #s
#define STRINGIZE(s) STRINGIZE2(s)

#include "Generated Files\version_gen.h"

#define FILE_VERSION VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION, VERSION_BUILD
#define FILE_VERSION_STRING  \
    STRINGIZE(VERSION_MAJOR) \
    "." STRINGIZE(VERSION_MINOR) "." STRINGIZE(VERSION_REVISION) "." STRINGIZE(VERSION_BUILD)
#define PRODUCT_VERSION FILE_VERSION
#define PRODUCT_VERSION_STRING FILE_VERSION_STRING

#define COMPANY_NAME "Microsoft Corporation"
#define COPYRIGHT_NOTE "Copyright (C) Microsoft Corporation. All rights reserved."
#define PRODUCT_NAME "PowerToys"

#include <string>

enum class version_architecture
{
    x64,
    arm
};

version_architecture get_current_architecture();
const wchar_t* get_architecture_string(const version_architecture);

inline std::wstring get_product_version(bool includeV = true)
{
    std::wstring version = includeV ? L"v" : L"";
    version += std::to_wstring(VERSION_MAJOR);
    version += L".";
    version += std::to_wstring(VERSION_MINOR);
    version += L".";
    version += std::to_wstring(VERSION_REVISION);
    if constexpr (VERSION_BUILD != 0 && VERSION_PREVIEW_SUFFIX[0] == L'\0')
    {
        // [fork-identity] 仅非 preview 渠道拼接第四段（内部 build）；preview 渠道的
        // VERSION_BUILD 是 preview 序号，已体现在 -preview.N 后缀，不重复拼接
        // （否则显示为 v0.1.0.2-preview.2）。
        version += L".";
        version += std::to_wstring(VERSION_BUILD);
    }

    // [fork-identity] 显示版本带 preview 后缀（如 v0.1.0-preview.1）；get_std_product_version 保持纯数字供比较。
    version += VERSION_PREVIEW_SUFFIX;

    return version;
}

inline std::wstring get_std_product_version(bool includeV = true)
{
    std::wstring version = includeV ? L"v" : L"";
    version += std::to_wstring(VERSION_MAJOR);
    version += L".";
    version += std::to_wstring(VERSION_MINOR);
    version += L".";
    version += std::to_wstring(VERSION_REVISION);
    version += L".";
    version += std::to_wstring(VERSION_BUILD);

    return version;
}

inline std::wstring get_product_version_channel()
{
    return VERSION_CHANNEL;
}

inline std::wstring get_product_version_source_commit()
{
    return VERSION_SOURCE_COMMIT;
}
