// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 快捷动作执行结果。
namespace CuinQuickActions.Common.Actions
{
    public sealed record QuickActionExecutionResult(QuickActionExecutionStatus Status, QuickActionDefinition? Action = null);
}
