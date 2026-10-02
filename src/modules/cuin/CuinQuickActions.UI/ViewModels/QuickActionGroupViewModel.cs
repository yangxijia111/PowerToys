// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 动作分组展示模型。
using System.Collections.Generic;

namespace CuinQuickActions.UI.ViewModels
{
    public sealed class QuickActionGroupViewModel
    {
        public string Title { get; }

        public IReadOnlyList<QuickActionItemViewModel> Items { get; }

        public QuickActionGroupViewModel(string title, IReadOnlyList<QuickActionItemViewModel> items)
        {
            Title = title;
            Items = items;
        }
    }
}
