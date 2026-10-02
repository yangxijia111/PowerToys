// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 面板主 ViewModel：把 ActionCatalog 组织为本地化的分组卡片列表。
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CuinQuickActions.Common.Actions;
using ResourceLoader = Microsoft.Windows.ApplicationModel.Resources.ResourceLoader;

namespace CuinQuickActions.UI.ViewModels
{
    public sealed class MainWindowViewModel
    {
        private readonly QuickActionRunner _runner;

        public IReadOnlyList<QuickActionGroupViewModel> Groups { get; }

        public MainWindowViewModel(QuickActionRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));

            var loader = new ResourceLoader("PowerToys.CuinQuickActions.UI.pri");
            var groups = new List<QuickActionGroupViewModel>();

            foreach (QuickActionGroup group in Enum.GetValues<QuickActionGroup>())
            {
                var items = new List<QuickActionItemViewModel>();
                foreach (var definition in ActionCatalog.GetByGroup(group))
                {
                    items.Add(new QuickActionItemViewModel(
                        definition,
                        loader.GetString(definition.TitleResourceKey),
                        loader.GetString(definition.DescriptionResourceKey)));
                }

                groups.Add(new QuickActionGroupViewModel(
                    loader.GetString($"Panel_Group_{group}"),
                    items));
            }

            Groups = groups;
        }

        /// <summary>该动作是否需要弹出确认对话框（危险操作）。</summary>
        public static bool NeedsConfirmation(QuickActionItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return QuickActionRunner.RequiresConfirmation(item.Definition);
        }

        public Task<QuickActionExecutionResult> ExecuteAsync(QuickActionItemViewModel item, bool userConfirmed)
        {
            ArgumentNullException.ThrowIfNull(item);
            return _runner.ExecuteAsync(item.Definition, userConfirmed);
        }
    }
}
