// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] Cuin Quick Actions 的跨进程常量。
// 事件名必须与 src/common/interop/shared_constants.h 中的
// CUIN_QUICK_ACTIONS_SHOW_EVENT / CUIN_QUICK_ACTIONS_TERMINATE_EVENT 完全一致，
// 由 tools/check_cuin_modules.py 静态校验两侧字面量。
namespace CuinQuickActions.Common
{
    public static class CuinConstants
    {
        public const string ModuleName = "CuinQuickActions";

        public const string ShowEventName = @"Local\PowerToysCuin-QuickActions-ShowEvent-4f3a9c2e-8b1d-4e6f-9a7c-5d2e8f1a3b4c";

        public const string TerminateEventName = @"Local\PowerToysCuin-QuickActions-TerminateEvent-9e5f2a7c-3d4b-4c8e-8f1a-6b2d9e4c7a5f";
    }
}
