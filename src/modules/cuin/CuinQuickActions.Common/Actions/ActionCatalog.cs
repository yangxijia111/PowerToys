// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 快捷动作目录：全部动作的静态、编译期定义。
// 安全设计：只调用 Windows 已提供的系统页面/程序（ms-settings: URI 与固定 exe），
// 不重复实现系统功能；所有目标均为常量，无任何自由文本输入。
using System;
using System.Collections.Generic;
using System.Linq;

namespace CuinQuickActions.Common.Actions
{
    public static class ActionCatalog
    {
        public static IReadOnlyList<QuickActionDefinition> All { get; } = new[]
        {
            // ===== 系统 =====
            new QuickActionDefinition
            {
                Id = "open_task_manager",
                Kind = QuickActionKind.LaunchShellTarget,
                Group = QuickActionGroup.System,
                Glyph = "\uE9D9", // Diagnostic
                ShellTarget = new ShellTarget { Executable = "taskmgr.exe" },
            },
            new QuickActionDefinition
            {
                Id = "lock_screen",
                Kind = QuickActionKind.LockWorkStation,
                Group = QuickActionGroup.System,
                Glyph = "\uE72E", // Lock
            },

            // ===== 维护 =====
            new QuickActionDefinition
            {
                Id = "close_foreground_app",
                Kind = QuickActionKind.CloseForegroundApplication,
                Group = QuickActionGroup.Maintenance,
                Glyph = "\uE74D", // Delete
                IsDestructive = true,
            },
            new QuickActionDefinition
            {
                Id = "restart_explorer",
                Kind = QuickActionKind.RestartExplorer,
                Group = QuickActionGroup.Maintenance,
                Glyph = "\uE72C", // Refresh
                IsDestructive = true,
            },
            new QuickActionDefinition
            {
                Id = "clear_clipboard",
                Kind = QuickActionKind.ClearClipboard,
                Group = QuickActionGroup.Maintenance,
                Glyph = "\uE8C8", // Copy
            },

            // ===== 系统设置 =====
            new QuickActionDefinition
            {
                Id = "open_windows_settings",
                Kind = QuickActionKind.LaunchUri,
                Group = QuickActionGroup.SettingsPage,
                Glyph = "\uE713", // Setting
                Uri = "ms-settings:",
            },
            new QuickActionDefinition
            {
                Id = "open_network_settings",
                Kind = QuickActionKind.LaunchUri,
                Group = QuickActionGroup.SettingsPage,
                Glyph = "\uE701", // Wifi
                Uri = "ms-settings:network",
            },
            new QuickActionDefinition
            {
                Id = "open_apps_features",
                Kind = QuickActionKind.LaunchUri,
                Group = QuickActionGroup.SettingsPage,
                Glyph = "\uE71D", // AllApps
                Uri = "ms-settings:appsfeatures",
            },
            new QuickActionDefinition
            {
                Id = "open_startup_apps",
                Kind = QuickActionKind.LaunchUri,
                Group = QuickActionGroup.SettingsPage,
                Glyph = "\uE7E8", // PowerButton
                Uri = "ms-settings:startupapps",
            },
            new QuickActionDefinition
            {
                Id = "edit_environment_variables",
                Kind = QuickActionKind.LaunchShellTarget,
                Group = QuickActionGroup.SettingsPage,
                Glyph = "\uE943", // Code
                ShellTarget = new ShellTarget { Executable = "rundll32.exe", Arguments = "sysdm.cpl,EditEnvironmentVariables" },
            },
            new QuickActionDefinition
            {
                Id = "open_hosts_file",
                Kind = QuickActionKind.LaunchShellTarget,
                Group = QuickActionGroup.SettingsPage,
                Glyph = "\uE8A5", // Document
                ShellTarget = new ShellTarget { Executable = "notepad.exe", Arguments = @"%SystemRoot%\System32\drivers\etc\hosts" },
            },

            // ===== 工具 =====
            new QuickActionDefinition
            {
                Id = "open_cuin_settings",
                Kind = QuickActionKind.OpenCuinSettings,
                Group = QuickActionGroup.Tools,
                Glyph = "\uE7EF", // Admin
            },
        };

        public static QuickActionDefinition? FindById(string id)
        {
            return All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
        }

        public static IReadOnlyList<QuickActionDefinition> GetByGroup(QuickActionGroup group)
        {
            return All.Where(a => a.Group == group).ToList();
        }
    }
}
