#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""sparse MSIX 注册路径防回归校验（Phase 6，docs/CODE_SIGNING.md §5.3）。

设计事实：安装器对 sparse MSIX（PowerToysSparse.msix）的注册是"无条件尝试 +
失败捕获降级"——unsigned 时 Windows 拒绝注册（正确行为），安装继续；
签名生效后注册自动成功，Win11 新式右键菜单恢复，无需代码分支。

本脚本静态断言该机制未被破坏：

    python tools/check_sparse_registration.py

断言项：
1. CustomAction.cpp 保留 InstallPackageIdentityMSIXCA，且引用 PowerToysSparse.msix；
2. 注册失败路径只记日志（Logger::error/warn），不得让 CA 返回失败或中止安装
   （否则 unsigned Preview 将无法安全降级）；
3. 不存在 FORK_DISABLE_SPARSE / FORK_SKIP_SPARSE 等禁用守卫——
   如确需禁用必须显式更新本脚本与 docs/CODE_SIGNING.md；
4. package.h 的 RegisterSparsePackage 保持失败返回 false 的上游语义。
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
CA = REPO / "installer" / "PowerToysSetupCustomActionsVNext" / "CustomAction.cpp"
PKG = REPO / "src" / "common" / "utils" / "package.h"

errors: list[str] = []


def check(name: str, ok: bool, detail: str = "") -> None:
    mark = "OK  " if ok else "FAIL"
    print(f"{mark} {name}" + (f" — {detail}" if detail and not ok else ""))
    if not ok:
        errors.append(name)


def main() -> int:
    ca_text = CA.read_text(encoding="utf-8", errors="replace")
    pkg_text = PKG.read_text(encoding="utf-8", errors="replace")

    # 1. 注册入口存在
    m = re.search(
        r"UINT __stdcall InstallPackageIdentityMSIXCA\(MSIHANDLE hInstall\)(.*?)\n}",
        ca_text,
        re.S,
    )
    check("InstallPackageIdentityMSIXCA 存在", m is not None)
    body = m.group(1) if m else ""
    check(
        "注册路径引用 PowerToysSparse.msix",
        "PowerToysSparse.msix" in body,
    )
    check(
        "perUser 注册调用 AddPackageByUriAsync 存在",
        "AddPackageByUriAsync" in body,
    )
    check(
        "perMachine 注册调用 StagePackageByUriAsync 存在",
        "StagePackageByUriAsync" in body,
    )

    # 2. 注册失败不得中止安装：失败分支只允许 Logger 记录。
    #    提取函数体内所有 if/catch 错误分支，禁止出现 hr 赋失败值或 ExitOnFailure。
    fail_hard = re.findall(r"hr\s*=\s*(?:E_|HRESULT_FROM_WIN32|EXIT_ON)", body)
    check(
        "注册失败仅记日志（不置 hr / 不 ExitOnFailure）",
        not fail_hard,
        f"发现 {fail_hard}",
    )
    # 函数结尾必须保持成功返回语义（SUCCEEDED(hr) → ERROR_SUCCESS）
    check(
        "CA 结尾维持 SUCCEEDED(hr) 语义",
        "er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE" in body,
    )

    # 3. 无 fork 禁用守卫
    guards = re.findall(r"FORK_(?:DISABLE|SKIP)_SPARSE\w*", ca_text + pkg_text)
    check("无 FORK_DISABLE_SPARSE / FORK_SKIP_SPARSE 守卫", not guards, str(guards))

    # 4. package.h 上游语义保持：RegisterSparsePackage 失败返回 false
    pkg_m = re.search(
        r"inline bool RegisterSparsePackage\(", pkg_text
    )
    check("package.h RegisterSparsePackage 存在", pkg_m is not None)
    if pkg_m:
        seg = pkg_text[pkg_m.start():]
        seg = seg[: seg.find("inline bool UnRegisterPackage")]
        check(
            "RegisterSparsePackage 失败路径 return false",
            "return false" in seg,
        )

    print()
    if errors:
        print(f"FAILED: {len(errors)} 项不通过")
        return 1
    print("OK: sparse MSIX 注册降级/恢复机制校验全部通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
