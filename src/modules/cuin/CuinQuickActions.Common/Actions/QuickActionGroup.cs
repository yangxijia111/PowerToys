// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 面板中的动作分组（UI 展示顺序 = 枚举顺序）。
namespace CuinQuickActions.Common.Actions
{
    public enum QuickActionGroup
    {
        /// <summary>系统。</summary>
        System,

        /// <summary>维护。</summary>
        Maintenance,

        /// <summary>系统设置。</summary>
        SettingsPage,

        /// <summary>工具。</summary>
        Tools,
    }
}
