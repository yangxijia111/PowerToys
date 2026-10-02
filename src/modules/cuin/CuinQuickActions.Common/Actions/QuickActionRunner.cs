// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 动作执行策略：危险动作必须先经用户确认；
// 即使 UI 层遗漏确认，这里也会拒绝执行（防御性第二道闸）。
using System;
using System.Threading.Tasks;

namespace CuinQuickActions.Common.Actions
{
    public sealed class QuickActionRunner
    {
        private readonly IQuickActionExecutor _executor;

        public QuickActionRunner(IQuickActionExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>该动作是否需要用户确认（危险操作）。</summary>
        public static bool RequiresConfirmation(QuickActionDefinition action)
        {
            return action.IsDestructive;
        }

        /// <summary>
        /// 执行动作。userConfirmed 表示 UI 层已经过明确的确认对话框取得用户同意；
        /// 危险动作在未确认时一律拒绝，不会触达执行器。
        /// </summary>
        public async Task<QuickActionExecutionResult> ExecuteAsync(QuickActionDefinition action, bool userConfirmed)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (ActionCatalog.FindById(action.Id) is null)
            {
                return new QuickActionExecutionResult(QuickActionExecutionStatus.UnknownAction, action);
            }

            if (action.IsDestructive && !userConfirmed)
            {
                return new QuickActionExecutionResult(QuickActionExecutionStatus.RejectedNeedsConfirmation, action);
            }

            try
            {
                var ok = await _executor.ExecuteAsync(action);
                return new QuickActionExecutionResult(ok ? QuickActionExecutionStatus.Success : QuickActionExecutionStatus.Failed, action);
            }
            catch (Exception)
            {
                // 执行器抛出的任何异常都视为失败，不让面板崩溃。
                return new QuickActionExecutionResult(QuickActionExecutionStatus.Failed, action);
            }
        }
    }
}
