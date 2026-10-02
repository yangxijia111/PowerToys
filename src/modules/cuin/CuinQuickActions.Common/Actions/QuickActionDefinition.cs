// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 单个快捷动作的静态定义（不可变数据）。
namespace CuinQuickActions.Common.Actions
{
    public sealed record QuickActionDefinition
    {
        /// <summary>稳定 ID（语义化 snake_case，同时用于 resw key 与测试断言）。</summary>
        public required string Id { get; init; }

        public required QuickActionKind Kind { get; init; }

        public required QuickActionGroup Group { get; init; }

        /// <summary>Segoe Fluent Icons 字形码（如 "\uE713"）。</summary>
        public required string Glyph { get; init; }

        /// <summary>危险操作：必须经用户确认后才允许执行（UI 红色标识）。</summary>
        public bool IsDestructive { get; init; }

        /// <summary>LaunchUri 的目标 URI。</summary>
        public string? Uri { get; init; }

        /// <summary>LaunchShellTarget 的目标进程。</summary>
        public ShellTarget? ShellTarget { get; init; }

        /// <summary>该动作需要管理员权限时为 true（当前目录中所有动作均为 false，
        /// 未来新增提权动作必须按需提权而非常驻提权运行）。</summary>
        public bool RequiresElevation { get; init; }

        // resw key 约定：由目录保证命名稳定，测试断言其存在。
        public string TitleResourceKey => $"QuickAction_{Id}_Title";

        public string DescriptionResourceKey => $"QuickAction_{Id}_Description";
    }
}
