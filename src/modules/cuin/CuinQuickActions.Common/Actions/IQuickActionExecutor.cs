// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 动作执行器抽象：业务策略在 QuickActionRunner，
// Windows API / Shell 调用在 UI 进程的 WindowsQuickActionExecutor 实现——分层以保可测性。
using System.Threading.Tasks;

namespace CuinQuickActions.Common.Actions
{
    public interface IQuickActionExecutor
    {
        /// <summary>执行动作。返回是否成功（失败原因记录到日志）。</summary>
        Task<bool> ExecuteAsync(QuickActionDefinition action);
    }
}
