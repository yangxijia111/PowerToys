// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// 场景推荐配置页：根据使用场景（开发/学习/办公/设计/通用）一键启用推荐工具组合。
    /// </summary>
    public sealed partial class PresetsPage : NavigablePage
    {
        private PresetsViewModel ViewModel { get; set; }

        public PresetsPage()
        {
            InitializeComponent();
            ViewModel = new PresetsViewModel();
            DataContext = ViewModel;
        }
    }
}
