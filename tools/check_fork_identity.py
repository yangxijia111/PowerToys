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

# Windows CI 控制台可能默认 cp1252，输出含中文的断言信息前强制 UTF-8（不可编码时降级而非崩溃）
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

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
    # Phase 4：官方码允许以 OFFICIAL_ 检测锚常量形式出现（并存检测），不得作为 fork 自身身份
    check("MsiUtils.h 官方码仅以检测锚常量出现",
          f"OFFICIAL_POWER_TOYS_UPGRADE_CODE[] = L\"{{{OFFICIAL_UPGRADE_CODE_MACHINE}}}\"" in msi_utils
          and f"OFFICIAL_POWER_TOYS_UPGRADE_CODE_USER[] = L\"{{{OFFICIAL_UPGRADE_CODE_USER}}}\"" in msi_utils)
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
        check(f"{rel} pipe 签名校验走统一宏", "requireMicrosoftSignature = FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE != 0" in text)

    # --- DSC 检测名 ---
    dsc = read("src/dsc/PowerToys.Settings.DSC.Schema.Generator/DSCGeneration.cs")
    check("DSCGeneration.cs 卸载项检测名为 fork 品牌", 'DisplayName -eq "%s"' % FORK_PRODUCT_NAME in dsc and '"PowerToys (Preview)"' not in dsc)

    # --- Phase 4：版本体系（单一来源 src/Version.props） ---
    version_props = read("src/Version.props")
    check("Version.props 版本为 0.1.0", "<Version>0.1.0</Version>" in version_props)
    check("Version.props 渠道为 preview", "<VersionChannel>preview</VersionChannel>" in version_props)
    check("Version.props preview 序号为 1", "<VersionPreview>1</VersionPreview>" in version_props)
    check("版本生成链含 preview 后缀宏", "VERSION_PREVIEW_SUFFIX" in read("src/common/version/version.vcxproj"))
    check("显示版本拼接 preview 后缀", "VERSION_PREVIEW_SUFFIX" in read("src/common/version/version.h"))

    # 发行资产命名（perUser/perMachine 必须不同名；完整防回归检查见 tools/check_release_assets.py）
    check("perUser MSI 产物名带 -perUser 后缀",
          'MSIName="PowerToysCuin-$(var.VersionFile)-$(var.PowerToysPlatform)-perUser.msi"' in common_wxi)
    check("perMachine MSI 产物名带 -perMachine 后缀",
          'MSIName="PowerToysCuin-$(var.VersionFile)-$(var.PowerToysPlatform)-perMachine.msi"' in common_wxi)
    installer_wixproj = read("installer/PowerToysSetupVNext/PowerToysInstallerVNext.wixproj")
    check("MSI 工程两个 scope 的 OutputName 区分 -perUser/-perMachine 后缀",
          "PowerToysCuin-$(VersionFile)-$(Platform)-perUser" in installer_wixproj
          and "PowerToysCuin-$(VersionFile)-$(Platform)-perMachine" in installer_wixproj)
    check("Bootstrapper OutputName 为 PowerToysCuin",
          "PowerToysCuin-$(VersionFile)-$(Platform)" in read("installer/PowerToysSetupVNext/PowerToysBootstrapperVNext.wixproj"))

    # 不安全自更新禁用（检查/下载执行分离）
    update_utils = read("src/runner/UpdateUtils.cpp")
    check("自动更新安装已禁用（FORK_AUTO_UPDATE_INSTALL_ENABLED=false）",
          "FORK_AUTO_UPDATE_INSTALL_ENABLED = false" in update_utils)
    check("更新入口重定向到 fork Releases 页", "FORK_RELEASES_PAGE_URL" in update_utils and "yangxijia111/PowerToys/releases" in update_utils)

    # 官方后装运行时检测（仅提示，不动官方）
    msi_utils_text = msi_utils
    check("官方并存检测锚存在（仅检测用）",
          "OFFICIAL_POWER_TOYS_UPGRADE_CODE" in msi_utils_text and "IsOfficialPowerToysInstalled" in msi_utils_text)
    check("runner 启动时给出并存提示", "IsOfficialPowerToysInstalled" in read("src/runner/main.cpp"))

    # IPC 签名降级与统一配置入口
    check("pipe 签名校验走统一宏", "FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE" in read("src/runner/settings_window.cpp")
          and "FORK_PIPE_REQUIRE_MICROSOFT_SIGNATURE" in read("src/runner/quick_access_host.cpp"))
    check("签名配置入口存在且默认关闭", (REPO / "ForkSigning.props").exists()
          and "ForkCodeSignEnabled" in (REPO / "ForkSigning.props").read_text(encoding="utf-8"))

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
