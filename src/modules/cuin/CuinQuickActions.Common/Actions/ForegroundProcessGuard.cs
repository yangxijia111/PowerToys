// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 结束前台应用的保护名单：拒绝终止 Windows 关键系统进程与自身。
using System;
using System.Collections.Generic;

namespace CuinQuickActions.Common.Actions
{
    public static class ForegroundProcessGuard
    {
        // Windows 关键系统进程（终止会导致系统不稳定/黑屏/蓝屏），一律拒绝。
        // 名单统一存“去掉 .exe 后缀”的基础进程名（System/MemCompression 本就无后缀）。
        private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "winlogon",
            "csrss",
            "services",
            "lsass",
            "smss",
            "wininit",
            "svchost",
            "dwm",
            "explorer", // 重启 Explorer 请使用专用的 restart_explorer 动作（带确认）
            "system",
            "memcompression",
            "fontdrvhost",
            "runtimebroker",
            "sihost",
            "taskhostw",
            "ctfmon",
            "audiodg",
            "searchindexer",
            "spoolsv",
        };

        // 面板自身与 PowerToys 进程也必须保护（基础名，同上）。
        private static readonly HashSet<string> SelfProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "powertoys.cuinquickactions.ui",
            "powertoys",
        };

        /// <summary>判断进程名（可含 .exe 后缀，大小写不敏感）是否受保护、禁止通过面板终止。</summary>
        public static bool IsProtectedProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return true; // 未知进程按受保护处理（最小权限原则）
            }

            var normalized = processName.Trim();
            if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^4];
            }

            return ProtectedProcesses.Contains(normalized) || SelfProcesses.Contains(normalized);
        }
    }
}
