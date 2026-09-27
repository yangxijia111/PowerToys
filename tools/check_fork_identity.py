#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Fork 发行身份静态校验（Phase 3）。

校验 docs/IDENTITY_MAP.md 中记录的身份修改真实落在代码里，且官方身份值
没有残留在 fork 自身身份位置。CI / 本地均可运行：

    python tools/check_fork_identity.py

任何断言失败都以非零码退出。修改身份项时必须同步更新本脚本
（维护规则见 docs/IDENTITY_MAP.md §4）。
"""

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent

FORK_UPGRADE_CODE_MACHINE = "78975C14-0AA0-41A7-99C2-55F44200A919"
FORK_UPGRADE_CODE_USER = "DA33EB25-63A5-4E9A-8B04-D8AE81BB888D"
FORK_BUNDLE_UPGRADE_CODE = "0A739D71-3045-42E6-8505-9DB32432F8E6"

OFFICIAL_UPGRADE_CODE_MACHINE = "42B84BF7-5FBF-473B-9C8B-049DC16F7708"
OFFICIAL_UPGRADE_CODE_USER = "D8B559DB-4C98-487A-A33F-50A8EEE42726"

FORK_APPDATA_FOLDER = "PowerToysCuin"
FORK_PRODUCT_NAME = "PowerToys Cuin (Community Edition)"
FORK_REPO = "yangxijia111/PowerToys"

errors: list[str] = []


def read(rel: str) -> str:
    return (REPO / rel).read_text(encoding="utf-8", errors="replace")


def check(desc: str, ok: bool) -> None:
    print(("PASS  " if ok else "FAIL  ") + desc)
    if not ok:
        errors.append(desc)


def main() -> int:
    # --- 安装器身份（Common.wxi 两个 scope 的 UpgradeCode） ---
    common_wxi = read("installer/PowerToysSetupVNext/Common.wxi")
    per_user_block = common_wxi.split('<?if $(var.PerUser) = "true"?>')[1].split("<?else?>")[0]
    machine_block = common_wxi.split("<?else?>")[-1].split("<?endif?>")[0]

    def defined_upgrade_code(block: str) -> str | None:
        m = re.search(r'<\?define UpgradeCodeGUID="([^"]+)"\s*\?>', block)
        return m.group(1).upper() if m else None

    check(
        "Common.wxi perUser UpgradeCode 为 fork 值",
        defined_upgrade_code(per_user_block) == FORK_UPGRADE_CODE_USER.upper(),
    )
    check(
        "Common.wxi perMachine UpgradeCode 为 fork 值",
        defined_upgrade_code(machine_block) == FORK_UPGRADE_CODE_MACHINE.upper(),
    )

    # --- Bundle 身份 ---
    bundle_wxs = read("installer/PowerToysSetupVNext/PowerToys.wxs")
    check(
        "PowerToys.wxs Bundle UpgradeCode 为 fork 值",
        f'<?define UpgradeCode="{FORK_BUNDLE_UPGRADE_CODE}"' in bundle_wxs,
    )
    check("PowerToys.wxs Bundle 名为 fork 品牌", FORK_PRODUCT_NAME in bundle_wxs)
    check("PowerToys.wxs Bundle Manufacturer 非 Microsoft", 'Manufacturer="PowerToys Cuin Community"' in bundle_wxs)
    check("PowerToys.wxs 安装目录为 PowerToysCuin", "PowerToysCuin" in bundle_wxs)

    # 官方码只允许出现在"检测锚"位置（ProductSearch），不得作为 bundle 身份
    official_as_identity = re.search(r'Bundle[^>]*UpgradeCode="(6341382d|%s|%s)' % (OFFICIAL_UPGRADE_CODE_MACHINE, OFFICIAL_UPGRADE_CODE_USER), bundle_wxs, re.I)
    check("PowerToys.wxs Bundle 身份不含官方码", official_as_identity is None)
    check(
        "PowerToys.wxs 保留官方码 ProductSearch 检测锚",
        bundle_wxs.count(OFFICIAL_UPGRADE_CODE_MACHINE) == 1 and bundle_wxs.count(OFFICIAL_UPGRADE_CODE_USER) == 1,
    )

    # --- MSI 身份与官方检测 ---
    product_wxs = read("installer/PowerToysSetupVNext/Product.wxs")
    check("Product.wxs Manufacturer 非 Microsoft", 'Manufacturer="PowerToys Cuin Community"' in product_wxs)
    check("Product.wxs MSI Name 为 fork 品牌", f'Name="{FORK_PRODUCT_NAME}"' in product_wxs)
    check("Product.wxs 安装目录为 PowerToysCuin", '<Directory Id="INSTALLFOLDER" Name="PowerToysCuin">' in product_wxs)
    check(
        "Product.wxs 官方码仅用于 OnlyDetect 升级检测",
        product_wxs.count(OFFICIAL_UPGRADE_CODE_MACHINE) == 1
        and product_wxs.count(OFFICIAL_UPGRADE_CODE_USER) == 1
        and 'Property="OFFICIALPOWERTOYSMACHINEDETECTED"' in product_wxs,
    )
    check(
        "Product.wxs MSI 身份不含官方码",
        f'UpgradeCode="$(var.UpgradeCodeGUID)"' in product_wxs,
    )

    core_wxs = read("installer/PowerToysSetupVNext/Core.wxs")
    check("Core.wxs 快捷方式名为 PowerToys Cuin", 'Name="PowerToys Cuin"' in core_wxs)
    check("Core.wxs 不再使用官方快捷方式名", 'Name="PowerToys (Preview)"' not in core_wxs)

    # --- 运行时 MSI 身份常量（MsiUtils.h 与 CustomAction.cpp 一致且为 fork 值） ---
    msi_utils = read("src/common/utils/MsiUtils.h")
    custom_action = read("installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp")
    check("MsiUtils.h UpgradeCode 为 fork 值", FORK_UPGRADE_CODE_MACHINE in msi_utils and FORK_UPGRADE_CODE_USER in msi_utils)
    check("MsiUtils.h 不含官方码", OFFICIAL_UPGRADE_CODE_MACHINE not in msi_utils and OFFICIAL_UPGRADE_CODE_USER not in msi_utils)
    check(
        "CustomAction.cpp 与 MsiUtils.h 身份常量一致",
        FORK_UPGRADE_CODE_MACHINE in custom_action and OFFICIAL_UPGRADE_CODE_MACHINE not in custom_action,
    )

    # --- AppData 隔离 ---
    shared_constants = read("src/common/interop/shared_constants.h")
    m = re.search(r'APPDATA_PATH\[\]\s*=\s*L"([^"]+)"', shared_constants)
    check("shared_constants.h APPDATA_PATH 为 fork 根", m is not None and m.group(1) == FORK_APPDATA_FOLDER)

    branding = read("src/common/ManagedCommon/Branding.cs")
    m = re.search(r'ForkAppDataFolderName\s*=\s*"([^"]+)"', branding)
    check("Branding.cs ForkAppDataFolderName 为 fork 根", m is not None and m.group(1) == FORK_APPDATA_FOLDER)

    setting_path = read("src/settings-ui/Settings.UI.Library/SettingPath.cs")
    check("SettingPath.cs 不再拼接官方路径", "Microsoft\\\\PowerToys" not in setting_path)
    check("SettingPath.cs 使用 Branding 常量", "Branding.ForkAppDataFolderName" in setting_path)

    etw = read("src/common/ManagedTelemetry/Telemetry/EtwTrace.cs")
    check("EtwTrace.cs ETW 目录为 fork 根", f'@"{FORK_APPDATA_FOLDER}"' in etw)

    # --- 更新端点 ---
    updating = read("src/common/updating/updating.cpp")
    check("updating.cpp 端点指向 fork 仓库", FORK_REPO in updating and "repos/microsoft/PowerToys" not in updating)

    installer_cpp = read("src/common/updating/installer.cpp")
    check("installer.cpp 产品名锚为 fork 品牌", 'POWERTOYS_PRODUCT_NAME_PREFIX = L"%s"' % FORK_PRODUCT_NAME in installer_cpp)

    # --- pipe 鉴权（fork 无微软签名，签名项必须关闭；目录/版本校验保留） ---
    for rel in ("src/runner/settings_window.cpp", "src/runner/quick_access_host.cpp"):
        text = read(rel)
        check(f"{rel} requireMicrosoftSignature = false", "requireMicrosoftSignature = false" in text)

    # --- DSC 检测名 ---
    dsc = read("src/dsc/PowerToys.Settings.DSC.Schema.Generator/DSCGeneration.cs")
    check("DSCGeneration.cs 卸载项检测名为 fork 品牌", 'DisplayName -eq "%s"' % FORK_PRODUCT_NAME in dsc and '"PowerToys (Preview)"' not in dsc)

    # --- fork 三个身份 GUID 不得在身份声明/检测锚之外复用为组件 GUID ---
    # PowerToys.wxs 是唯一合法引用处：Bundle 身份 define + fork 两个 MSI 码的 ProductSearch 检测锚。
    dup_hits = []
    for guid in (FORK_UPGRADE_CODE_MACHINE, FORK_UPGRADE_CODE_USER, FORK_BUNDLE_UPGRADE_CODE):
        for p in (REPO / "installer").rglob("*.wxs"):
            if p.name == "PowerToys.wxs":
                continue
            if guid in p.read_text(encoding="utf-8", errors="replace"):
                dup_hits.append(f"{guid} -> {p.name}")
    check("fork 身份 GUID 无异常复用", not dup_hits)

    print()
    if errors:
        print(f"FAILED: {len(errors)} 项不通过")
        return 1
    print("OK: fork 发行身份校验全部通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
