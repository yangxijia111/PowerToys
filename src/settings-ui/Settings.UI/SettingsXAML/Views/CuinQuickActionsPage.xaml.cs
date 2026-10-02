// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] Cuin Quick Actions 的 Settings 页 code-behind（抄自 PeekPage）。
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class CuinQuickActionsPage : NavigablePage, IRefreshablePage
    {
        private CuinQuickActionsViewModel ViewModel { get; set; }

        public CuinQuickActionsPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new CuinQuickActionsViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
