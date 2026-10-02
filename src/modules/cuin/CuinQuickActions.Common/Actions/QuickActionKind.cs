// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 快捷动作的执行类型。
namespace CuinQuickActions.Common.Actions
{
    public enum QuickActionKind
    {
        /// <summary>打开固定 URI（ms-settings: 等系统页面）。</summary>
        LaunchUri,

        /// <summary>启动固定可执行文件（可选固定参数，结构化构造，不接受用户输入）。</summary>
        LaunchShellTarget,

        /// <summary>锁定工作站（Win32 LockWorkStation）。</summary>
        LockWorkStation,

        /// <summary>清空剪贴板。</summary>
        ClearClipboard,

        /// <summary>结束呼出面板前的活动窗口程序（危险，需确认）。</summary>
        CloseForegroundApplication,

        /// <summary>重启 Windows 资源管理器（危险，需确认）。</summary>
        RestartExplorer,

        /// <summary>打开 PowerToys Cuin 设置窗口。</summary>
        OpenCuinSettings,
    }
}
