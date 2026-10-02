// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 动作卡片 / 分组的展示模型：定义来自 ActionCatalog，
// 标题与描述在构造时经 ResourceLoader 本地化（zh-CN / en-US）。
using System.Collections.Generic;
using CuinQuickActions.Common.Actions;

namespace CuinQuickActions.UI.ViewModels
{
    public sealed class QuickActionItemViewModel
    {
        public QuickActionDefinition Definition { get; }

        public string Title { get; }

        public string Description { get; }

        public string Glyph => Definition.Glyph;

        public bool IsDestructive => Definition.IsDestructive;

        public QuickActionItemViewModel(QuickActionDefinition definition, string title, string description)
        {
            Definition = definition;
            Title = title;
            Description = description;
        }
    }
}
