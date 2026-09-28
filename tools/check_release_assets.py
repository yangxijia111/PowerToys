#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Release 资产命名静态校验（Phase 5）。

防止 perUser / perMachine 两个 MSI 再次共用同一个最终文件名（历史上两者
OutputName 相同，仅靠 UserSetup/MachineSetup 子目录区分，收集资产时互相
覆盖）。本脚本把命名约定固化为一组断言，CI / 本地均可运行：

    python tools/check_release_assets.py

校验内容：
1. Version.props 是版本唯一来源（version / channel / preview 序号）；
2. MSI 工程两个 scope 的 OutputName 不同且分别带 -perUser / -perMachine 后缀；
3. Common.wxi 两个 scope 的 MSIName 与 wixproj OutputName 一致（Bundle 按该
   名字引用 MSI，两处不一致会导致 Bootstrapper 构建找不到文件）；
4. Bootstrapper OutputName 与两个 MSI 名互不冲突；
5. cuin-release.yml 中列出的四个最终资产名（EXE / perUser MSI / perMachine
   MSI / SHA256SUMS.txt）与以上推导完全一致，缺一即失败。

任何断言失败都以非零码退出。改动版本号或安装器命名时必须同步更新
cuin-release.yml（本脚本会强制发现遗漏）。
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

errors: list[str] = []


def read(rel: str) -> str:
    return (REPO / rel).read_text(encoding="utf-8", errors="replace")


def check(desc: str, ok: bool) -> None:
    print(("PASS  " if ok else "FAIL  ") + desc)
    if not ok:
        errors.append(desc)


def wixproj_output_names(text: str) -> dict[str, str]:
    """提取 wixproj 中按 PerUser 条件区分的 OutputName。"""
    names = {}
    for cond, scope in (("'$(PerUser)' == 'true'", "perUser"),
                        ("'$(PerUser)' != 'true'", "perMachine")):
        m = re.search(
            r'<OutputName\s+Condition\s*=\s*"\s*%s\s*"\s*>([^<]+)</OutputName>' % re.escape(cond),
            text,
        )
        if m:
            names[scope] = m.group(1).strip()
    return names


def wxi_msi_names(common_wxi: str) -> dict[str, str]:
    """提取 Common.wxi 两个 scope 的 MSIName。"""
    user_block = common_wxi.split('<?if $(var.PerUser) = "true"?>')[1].split("<?else?>")[0]
    machine_block = common_wxi.split("<?else?>")[-1].split("<?endif?>")[0]
    names = {}
    for block, scope in ((user_block, "perUser"), (machine_block, "perMachine")):
        m = re.search(r'<\?define MSIName="([^"]+)"\s*\?>', block)
        if m:
            names[scope] = m.group(1).strip()
    return names


def normalize(name: str) -> str:
    """把 wixproj / wxi 两套 MSBuild 占位符归一成同一形式再比较。"""
    return (name
            .replace("$(VersionFile)", "<VER>")
            .replace("$(var.VersionFile)", "<VER>")
            .replace("$(Platform)", "<PLAT>")
            .replace("$(var.PowerToysPlatform)", "<PLAT>"))


def concrete(version_file: str, template: str, platform: str = "x64") -> str:
    return (template
            .replace("$(VersionFile)", version_file)
            .replace("$(var.VersionFile)", version_file)
            .replace("$(Platform)", platform)
            .replace("$(var.PowerToysPlatform)", platform))


def main() -> int:
    # --- 版本唯一来源 ---
    version_props = read("src/Version.props")
    version = re.search(r"<Version>([^<]+)</Version>", version_props)
    channel = re.search(r"<VersionChannel>([^<]+)</VersionChannel>", version_props)
    preview = re.search(r"<VersionPreview>(\d+)</VersionPreview>", version_props)
    check("Version.props 提供 Version", version is not None)
    check("Version.props 渠道为 preview", channel is not None and channel.group(1) == "preview")
    check("Version.props 提供 VersionPreview 序号", preview is not None)
    if not (version and channel and preview):
        print("FAIL  无法推导版本，中止")
        return 1
    version_file = f"{version.group(1)}-preview.{preview.group(1)}"

    # --- MSI 工程命名 ---
    installer_wixproj = read("installer/PowerToysSetupVNext/PowerToysInstallerVNext.wixproj")
    out_names = wixproj_output_names(installer_wixproj)
    check("MSI 工程定义了 perUser / perMachine 两个 OutputName", set(out_names) == {"perUser", "perMachine"})
    if set(out_names) == {"perUser", "perMachine"}:
        check("perUser OutputName 带 -perUser 后缀", out_names["perUser"].endswith("-perUser"))
        check("perMachine OutputName 带 -perMachine 后缀", out_names["perMachine"].endswith("-perMachine"))
        check("perUser / perMachine OutputName 不同名", out_names["perUser"] != out_names["perMachine"])

    # --- Common.wxi 与 wixproj 一致（Bundle 引用正确性） ---
    common_wxi = read("installer/PowerToysSetupVNext/Common.wxi")
    msi_names = wxi_msi_names(common_wxi)
    check("Common.wxi 定义了 perUser / perMachine 两个 MSIName", set(msi_names) == {"perUser", "perMachine"})
    if set(out_names) == {"perUser", "perMachine"} and set(msi_names) == {"perUser", "perMachine"}:
        for scope in ("perUser", "perMachine"):
            check(
                f"Common.wxi {scope} MSIName 与 wixproj OutputName 一致",
                normalize(msi_names[scope]) == normalize(out_names[scope]) + ".msi",
            )

    # --- Bootstrapper 命名 ---
    boot_wixproj = read("installer/PowerToysSetupVNext/PowerToysBootstrapperVNext.wixproj")
    boot_names = wixproj_output_names(boot_wixproj)
    boot_default = re.search(r"<OutputName>([^<]+)</OutputName>", boot_wixproj)
    if set(out_names) == {"perUser", "perMachine"}:
        msi_norm = {normalize(n) for n in out_names.values()}
        for label, name in [("默认", boot_default.group(1).strip() if boot_default else None)] + \
                           [(s, n) for s, n in boot_names.items()]:
            check(
                f"Bootstrapper {label} OutputName 不与 MSI 冲突",
                name is not None and normalize(name) not in {m + ".msi" for m in msi_norm},
            )

    # --- Release workflow 期望资产与推导一致 ---
    workflow = read(".github/workflows/cuin-release.yml")
    if set(out_names) == {"perUser", "perMachine"} and boot_default:
        expected = {
            "EXE": concrete(version_file, boot_default.group(1).strip()) + ".exe",
            "perUser MSI": concrete(version_file, out_names["perUser"]) + ".msi",
            "perMachine MSI": concrete(version_file, out_names["perMachine"]) + ".msi",
        }
        for label, final_name in expected.items():
            check(f"cuin-release.yml 期望 {label} 资产名 {final_name}", final_name in workflow)
        check("cuin-release.yml 期望 SHA256SUMS.txt", "SHA256SUMS.txt" in workflow)
        print(f"\n预期发布资产（版本 {version_file}）：")
        for label, final_name in expected.items():
            print(f"  - {final_name}  ({label})")
        print("  - SHA256SUMS.txt")

    if errors:
        print(f"\n{len(errors)} 项失败")
        return 1
    print("\n全部通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
