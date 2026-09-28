#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""zh-CN 资源覆盖检查工具（本 Fork 二开自有工具）。

用法：
    python tools/check_zh_cn_coverage.py                 # 输出报告
    python tools/check_zh_cn_coverage.py --fail-on-orphan  # 存在孤儿 key 时返回非零退出码

检查逻辑（对应本 Fork 的"选择性覆盖"本地化策略，见 docs/DEVELOPMENT_PLAN.md 第 5 节）：
1. zh-CN 存在而 en-us 已不存在的 key = 孤儿 key。
   说明上游删除/重命名了资源而中文层没有跟随（"silently 丢失资源"），
   属于必须修复的问题（--fail-on-orphan 时导致非零退出码）。
2. en-us 存在而 zh-CN 未覆盖的 key = 缺失 key。
   这是策略允许的（运行时回退 en-us），仅输出清单供规划后续翻译，
   不作为错误。
3. 同名 key 的占位符（{0}/{1}）一致性校验：数量不一致视为孤儿级错误，
   避免运行时 FormatException。
"""

import argparse
import io
import re
import sys
import xml.etree.ElementTree as ET

# Windows CI 控制台可能默认 cp1252，输出含中文的断言信息前强制 UTF-8（不可编码时降级而非崩溃）
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

REPO_ROOT_HINT = "请在仓库根目录运行"
DEFAULT_EN = "src/settings-ui/Settings.UI/Strings/en-us/Resources.resw"
DEFAULT_ZH = "src/settings-ui/Settings.UI/Strings/zh-CN/Resources.resw"
PLACEHOLDER = re.compile(r"\{\d+\}")


def load_resw(path):
    """返回 {key: value}。文件不存在时返回 None。"""
    try:
        tree = ET.parse(path)
    except FileNotFoundError:
        return None
    root = tree.getroot()
    entries = {}
    for data in root.iter("data"):
        name = data.get("name")
        value_el = data.find("value")
        if name and value_el is not None and value_el.text is not None:
            entries[name] = value_el.text
    return entries


def main():
    parser = argparse.ArgumentParser(description="zh-CN 资源覆盖检查")
    parser.add_argument("--fail-on-orphan", action="store_true", help="存在孤儿 key 时退出码为 2")
    parser.add_argument("--en", default=DEFAULT_EN)
    parser.add_argument("--zh", default=DEFAULT_ZH)
    parser.add_argument("--missing-limit", type=int, default=40, help="缺失 key 最多打印条数")
    args = parser.parse_args()

    en = load_resw(args.en)
    zh = load_resw(args.zh)
    if en is None:
        print(f"[ERROR] 无法读取 en-us 资源: {args.en} ({REPO_ROOT_HINT})")
        return 1
    if zh is None:
        print(f"[WARN] zh-CN 资源不存在: {args.zh}（中文层尚未建立）")
        return 0

    orphans = sorted(set(zh) - set(en))
    missing = sorted(set(en) - set(zh))

    # 占位符一致性：zh 值的占位符多重集必须与 en 一致
    placeholder_issues = []
    for key in sorted(set(zh) & set(en)):
        if sorted(PLACEHOLDER.findall(zh[key])) != sorted(PLACEHOLDER.findall(en[key])):
            placeholder_issues.append(key)

    print("=" * 60)
    print("zh-CN 覆盖报告")
    print(f"  en-us keys : {len(en)}")
    print(f"  zh-CN keys : {len(zh)}")
    print(f"  覆盖率     : {len(set(zh) & set(en)) / len(en) * 100:.1f}%")
    print("=" * 60)

    status = 0

    if orphans:
        print(f"\n[孤儿 key] zh-CN 有而 en-us 无（必须修复，{len(orphans)} 个）:")
        for k in orphans:
            print("  -", k)
        status = 2
    else:
        print("\n[孤儿 key] 无 ✓")

    if placeholder_issues:
        print(f"\n[占位符不一致]（必须修复，{len(placeholder_issues)} 个）:")
        for k in placeholder_issues:
            print(f"  - {k}: en={PLACEHOLDER.findall(en[k])} zh={PLACEHOLDER.findall(zh[k])}")
        status = 2
    else:
        print("[占位符] 全部一致 ✓")

    print(f"\n[缺失 key] en-us 有而 zh-CN 未覆盖（{len(missing)} 个，运行时回退英文，非错误）:")
    for k in missing[: args.missing_limit]:
        print("  -", k)
    if len(missing) > args.missing_limit:
        print(f"  ... 其余 {len(missing) - args.missing_limit} 条省略")

    if args.fail_on_orphan and status != 0:
        print("\nRESULT: FAIL（存在孤儿 key 或占位符不一致）")
        return status
    print("\nRESULT: OK" if status == 0 else "\nRESULT: FAIL")
    return status


if __name__ == "__main__":
    sys.exit(main())
