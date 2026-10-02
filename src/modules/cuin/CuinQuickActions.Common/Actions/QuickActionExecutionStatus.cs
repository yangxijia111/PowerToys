// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 快捷动作执行结果状态。
namespace CuinQuickActions.Common.Actions
{
    public enum QuickActionExecutionStatus
    {
        /// <summary>已成功执行。</summary>
        Success,

        /// <summary>危险动作未经用户确认，已拒绝执行。</summary>
        RejectedNeedsConfirmation,

        /// <summary>执行失败（详细信息在日志中）。</summary>
        Failed,

        /// <summary>目录中不存在该动作。</summary>
        UnknownAction,
    }
}
