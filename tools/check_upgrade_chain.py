#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""preview.1 → preview.2 升级链静态校验（Phase 6，docs/POST_RELEASE_PLAN.md §6）。

目标：保证 preview.2 MSI 可以**覆盖升级** preview.1（不要求用户手动卸载），
且用户数据（Settings / OOBE / Presets）在升级与卸载后都保留。

    python tools/check_upgrade_chain.py

断言项（WiX 源级）：
1. perUser / perMachine UpgradeCode 为 fork 专属字面量且互不相同
   （UpgradeCode 跨版本不变是 Windows Installer 识别升级链的锚点）；
2. `<MajorUpgrade>` 存在（FindRelatedProducts + RemoveExistingProducts 语义）；
3. `<UpgradeVersion Property="PREVIOUSVERSIONSINSTALLED">` 存在（旧版本检测）；
4. `<Package>` 不显式指定 Product Id（保持 WiX 默认 `*` 自动生成——
   ProductCode 每次构建变化，允许同 UpgradeCode 下的 major upgrade）；
5. Bootstrapper Bundle UpgradeCode 为 fork 专属字面量（Burn 覆盖升级锚点）；
6. 安装器不删除/不搬移用户数据目录：WiX 源中不得出现指向
   LOCALAPPDATA / PowerToysCuin（AppData）的 RemoveFile / RemoveFolder /
   RemoveFileEx —— 卸载保留 `%LOCALAPPDATA%\\PowerToysCuin`（与官方一致）；
7. REINSTALLMODE=amus（升级时强制覆盖旧版本文件）；
8. MSI/Bundle 的 ProductVersion 引用 `$(var.MsiVersion)`（preview 序号编入
   第四位，0.1.0-preview.2 → 0.1.0.2），且两个 wixproj 均定义 MsiVersion 并
   引用 `$(VersionPreview)`、经 DefineConstants 传入。缺此编码时 preview.1
   与 preview.2 的 ProductVersion 同为 0.1.0：MajorUpgrade 的 UpgradeVersion
   （Maximum 含自身、不含等号）检测不到旧版、MSI 以 1638 拒绝同版本异
   ProductCode 安装、Bootstrapper 的 `TargetPowerToysVersion >= Detected*`
   条件拦截引导——三层全部挡住覆盖升级（2026-09-30 真机预演发现）；
9. Bootstrapper bal:Condition 使用 Burn 小写操作符（and/or/not）。
   Burn 条件解析器大小写敏感，大写 AND/OR 会在运行时解析失败
   （exit 13, 0x8007000d "Failed to parse condition"）——preview.1 已发布
   EXE 即因此从未成功运行过（2026-09-30 首次真机执行 Bootstrapper 发现；
   此前全部真机安装走 msiexec，EXE 路径无覆盖）；
10. 文件版本资源（FileVersion / VERSION_BUILD）把 preview 序号编入第四位。
    MSI 覆盖升级的文件替换按"现存文件版本 < 新文件版本"判定；Burn 引导路径
    下 MSI 内 Property 定义的 REINSTALLMODE=amus 不驱动 file costing，两版
    文件版本相同会 "Won't Overwrite; Existing file is of an equal version"
    ——注册表升级成功但二进制保留旧版（2026-09-30 发布 Gate 真机发现）。

真机（安装 preview.1 → 覆盖安装 preview.2 → 验证数据保留）属于 preview.2
发布前的 RELEASE_CHECKLIST §4 场景复验，不在本脚本范围。
"""

import re
import sys
from pathlib import Path

# Windows CI 控制台可能默认 cp1252，输出含中文的断言信息前强制 UTF-8
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

REPO = Path(__file__).resolve().parent.parent
COMMON = REPO / "installer" / "PowerToysSetupVNext" / "Common.wxi"
PRODUCT = REPO / "installer" / "PowerToysSetupVNext" / "Product.wxs"
BUNDLE = REPO / "installer" / "PowerToysSetupVNext" / "PowerToys.wxs"
WXS_DIR = REPO / "installer" / "PowerToysSetupVNext"

FORK_UPGRADE_USER = "DA33EB25-63A5-4E9A-8B04-D8AE81BB888D"
FORK_UPGRADE_MACHINE = "78975C14-0AA0-41A7-99C2-55F44200A919"
FORK_BUNDLE_UPGRADE = "0A739D71-3045-42E6-8505-9DB32432F8E6"
OFFICIAL_GUIDS = {
    "42B84BF7-5FBF-473B-9C8B-049DC16F7708",
    "D8B559DB-4C98-487A-A33F-50A8EEE42726",
}

errors: list[str] = []


def check(name: str, ok: bool, detail: str = "") -> None:
    mark = "OK  " if ok else "FAIL"
    print(f"{mark} {name}" + (f" — {detail}" if detail and not ok else ""))
    if not ok:
        errors.append(name)


def main() -> int:
    common = COMMON.read_text(encoding="utf-8", errors="replace")
    product = PRODUCT.read_text(encoding="utf-8", errors="replace")
    bundle = BUNDLE.read_text(encoding="utf-8", errors="replace")
    version_props_text = (REPO / "src" / "Version.props").read_text(encoding="utf-8", errors="replace")

    # 1. UpgradeCode 字面量（perUser / perMachine 区分）
    ordered = re.findall(r'UpgradeCodeGUID="([0-9A-Fa-f-]{36})"', common)
    check("UpgradeCode 为字面量且恰好两个（perUser/perMachine）", len(ordered) == 2)
    if len(ordered) == 2:
        check(
            "perUser/perMachine UpgradeCode 互不相同且均为 fork 专属",
            {ordered[0], ordered[1]} == {FORK_UPGRADE_USER, FORK_UPGRADE_MACHINE}
            and ordered[0] != ordered[1],
        )
    check(
        "UpgradeCode 不使用官方 GUID",
        not (set(ordered) & OFFICIAL_GUIDS),
    )

    # 2/3/7. MSI 升级语义
    check("<MajorUpgrade> 存在", "<MajorUpgrade" in product)
    # 11. MajorUpgrade Schedule=afterInstallExecute：默认 afterInstallValidate 下
    #     RemoveExistingProducts 先于 InstallFiles 执行，内容相同的无版本文件被
    #     costing 判定 Won't Overwrite 跳过后又被旧产品卸载物理删除（升级即丢文件）。
    #     2026-10-04 preview.2 → preview.3 真机升级实测丢失 1600+ 文件。
    check(
        "MajorUpgrade Schedule=afterInstallExecute（防升级丢文件）",
        re.search(r'<MajorUpgrade[^>]*Schedule="afterInstallExecute"', product) is not None,
    )
    # 12. ForceReinstallModeAmus：Burn 命令行 REINSTALLMODE=muso 覆盖 Property 表的 amus，
    #     costing 跳过 + REP 删除 = 升级丢文件（同上）。immediate CA 在 CostInitialize 前
    #     强制恢复 amus（'a'=全文件强制重装），晚于命令行属性应用，必然生效。
    check(
        "ForceReinstallModeAmus CA 存在并排在 CostInitialize 前",
        re.search(r'<CustomAction Id="ForceReinstallModeAmus" Property="REINSTALLMODE" Value="amus"', product) is not None
        and re.search(r'<Custom Action="ForceReinstallModeAmus" Before="CostInitialize" />', product) is not None,
    )
    check(
        "PREVIOUSVERSIONSINSTALLED 检测存在",
        'Property="PREVIOUSVERSIONSINSTALLED"' in product,
    )
    check("REINSTALLMODE=amus 存在", 'Id="REINSTALLMODE" Value="amus"' in product)

    # 4. ProductCode 保持自动生成：Package 元素不得显式指定 Id
    pkg_tag = re.search(r"<Package\s[^>]*>", product)
    check("<Package> 元素存在", pkg_tag is not None)
    if pkg_tag:
        check(
            "Package 未显式指定 Product Id（保持 `*` 自动生成）",
            not re.search(r'\bId\s*=', pkg_tag.group(0)),
            pkg_tag.group(0),
        )

    # 8. ProductVersion 编码 preview 序号：MSI/Bundle 的版本比较必须跨 preview 递增
    if pkg_tag:
        check(
            "Package Version 引用 $(var.MsiVersion)",
            'Version="$(var.MsiVersion)"' in pkg_tag.group(0),
            pkg_tag.group(0),
        )
    check(
        "UpgradeVersion Maximum 引用 $(var.MsiVersion)",
        re.search(r'<UpgradeVersion[^>]*Maximum="\$\(var\.MsiVersion\)"[^>]*Property="PREVIOUSVERSIONSINSTALLED"', product)
        is not None,
    )
    for wixproj_name in ("PowerToysInstallerVNext.wixproj", "PowerToysBootstrapperVNext.wixproj"):
        wixproj_text = (WXS_DIR / wixproj_name).read_text(encoding="utf-8", errors="replace")
        check(
            f"{wixproj_name} 定义 MsiVersion 并编码 $(VersionPreview)",
            re.search(
                r"<MsiVersion Condition=\"'\$\(VersionPreview\)' != '' and '\$\(VersionPreview\)' != '0'\">\$\(Version\)\.\$\(VersionPreview\)</MsiVersion>",
                wixproj_text,
            )
            is not None,
        )
        check(
            f"{wixproj_name} 经 DefineConstants 传入 MsiVersion",
            re.search(r"<DefineConstants>[^<]*MsiVersion=\$\(MsiVersion\)", wixproj_text) is not None,
        )

    # 5. Bootstrapper Bundle UpgradeCode：字面量 define + Bundle 元素引用该变量
    #    （PowerToys.wxs 中的官方 GUID 仅用于互斥检测 Upgrade 表，属预期，不作断言对象）
    b_define = re.findall(r'<\?define UpgradeCode="([0-9A-Fa-f-]{36})"\?>', bundle)
    check(
        "Bundle UpgradeCode define 为 fork 专属字面量",
        b_define == [FORK_BUNDLE_UPGRADE],
        str(b_define),
    )
    bundle_tag = re.search(r"<Bundle\s[^>]*>", bundle)
    check("<Bundle> 元素存在", bundle_tag is not None)
    if bundle_tag:
        check(
            "Bundle 引用 $(var.UpgradeCode)（不使用官方 GUID）",
            'UpgradeCode="$(var.UpgradeCode)"' in bundle_tag.group(0),
            bundle_tag.group(0),
        )
        check(
            "Bundle Version 引用 $(var.MsiVersion)",
            'Version="$(var.MsiVersion)"' in bundle_tag.group(0),
            bundle_tag.group(0),
        )
    check(
        "Bootstrapper TargetPowerToysVersion 引用 $(var.MsiVersion)",
        'Name="TargetPowerToysVersion" Type="version" Value="$(var.MsiVersion)"' in bundle,
    )

    # 6. 用户数据保留：任何 wxs/wxi 不得有指向 AppData 的 Remove* 元素
    offenders = []
    for p in WXS_DIR.glob("*.wx*"):
        text = p.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r"<(RemoveFile|RemoveFolder|RemoveFileEx)\b[^>]*>", text):
            if re.search(r"LOCALAPPDATA|PowerToysCuin", m.group(0), re.I):
                offenders.append(f"{p.name}: {m.group(0)}")
    check("无 Remove* 元素指向 LOCALAPPDATA / PowerToysCuin", not offenders, "; ".join(offenders[:3]))

    # 9. bal:Condition 必须使用 Burn 小写操作符 + 带引号字面量（大写操作符或
    #    未加引号的 0.0.0.0/19041 之类字面量都会在运行时解析失败 exit 13）
    bal_conditions = re.findall(r'<bal:Condition\b[^>]*Condition="([^"]*)"', bundle)
    check("bal:Condition 至少 5 个（互斥/同版本/系统检测）", len(bal_conditions) >= 5, str(len(bal_conditions)))
    upper_ops = [c for c in bal_conditions if re.search(r"\b(AND|OR|NOT)\b", c)]
    check(
        "bal:Condition 无大写 AND/OR/NOT（Burn 操作符必须小写）",
        not upper_ops,
        "; ".join(upper_ops[:2]),
    )
    bare_literals = [
        c
        for c in bal_conditions
        if re.search(r'=\s*[\d][\w.]*\s', c.replace("&quot;", '"').replace("&gt;", ">"))
        and not re.search(r'=\s*&quot;', c)
    ]
    check(
        "bal:Condition 字面量带引号（裸 0.0.0.0/19041 解析失败）",
        not bare_literals,
        "; ".join(bare_literals[:2]),
    )

    # 10. 文件版本资源编码 preview 序号（Version.props FileVersion + version.vcxproj VersionBuild）
    check(
        "Version.props 定义 FileVersion 并编码 $(VersionPreview)",
        re.search(
            r'<FileVersion Condition="\'\$\(VersionPreview\)\' != \'\' and \'\$\(VersionPreview\)\' != \'0\'">\$\(Version\)\.\$\(VersionPreview\)</FileVersion>',
            version_props_text,
        )
        is not None,
    )
    version_h_text = (REPO / "src" / "common" / "version" / "version.h").read_text(encoding="utf-8", errors="replace")
    check(
        "version.h 显示版本在 preview 渠道不拼 BUILD（避免 v0.1.0.2-preview.2）",
        "VERSION_BUILD != 0 && VERSION_PREVIEW_SUFFIX[0]" in version_h_text,
    )
    vcxproj_text = (REPO / "src" / "common" / "version" / "version.vcxproj").read_text(encoding="utf-8", errors="replace")
    check(
        "version.vcxproj 的 VersionBuild 编码 $(VersionPreview)",
        re.search(
            r"<VersionBuild Condition=\"'\$\(VersionPreview\)' != '' and '\$\(VersionPreview\)' != '0' and '\$\(VersionChannel\)' == 'preview'\">\$\(VersionPreview\)</VersionBuild>",
            vcxproj_text,
        )
        is not None,
    )

    print()
    if errors:
        print(f"FAILED: {len(errors)} 项不通过")
        return 1
    print("OK: preview 升级链静态校验全部通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
