// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Enums;
using Microsoft.PowerToys.Settings.UI.OOBE.ViewModel;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    /// <summary>
    /// [fork-brand] 首启场景推荐页：欢迎流程中的"快速设置"。
    /// 用户选择使用场景 → 确认推荐工具 → 一键启用（或跳过）。
    /// 只启用推荐工具，不关闭用户已启用的任何功能。
    /// </summary>
    public sealed partial class OobePresetPicker : Page
    {
        private readonly Windows.ApplicationModel.Resources.ResourceLoader _loader = ResourceLoaderInstance.ResourceLoader;

        private PresetDefinition _selectedPreset;

        private bool _applied;

        public OobePresetPicker()
        {
            InitializeComponent();

            var names = new List<string>();
            foreach (var preset in PresetsCatalog.All)
            {
                names.Add(_loader.GetString("Preset_" + preset.Id + "_Name"));
            }

            ScenarioSelector.ItemsSource = names;
            ViewModel = App.OobeShellViewModel.GetModule(PowerToysModules.PresetPicker);
            DataContext = this;
        }

        public OobePowerToysModule ViewModel { get; private set; }

        private void ScenarioSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_applied)
            {
                return;
            }

            var index = ScenarioSelector.SelectedIndex;
            if (index < 0 || index >= PresetsCatalog.All.Count)
            {
                Step2Panel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                Step3Panel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                return;
            }

            _selectedPreset = PresetsCatalog.All[index];

            // 展示推荐工具清单（本地化名称）
            var labels = new List<string>();
            foreach (var moduleType in _selectedPreset.EnableModules)
            {
                var key = ModuleHelper.GetModuleLabelResourceName(moduleType);
                var label = _loader.GetString(key);
                labels.Add(string.IsNullOrEmpty(label) ? moduleType.ToString() : label);
            }

            RecommendedList.ItemsSource = labels;
            Step2Panel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            Step3Panel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            AppliedInfoBar.IsOpen = false;
        }

        private async void ApplyButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (_selectedPreset == null || _applied)
            {
                return;
            }

            ApplyButton.IsEnabled = false;
            try
            {
                var changed = await PresetsService.ApplyAndSaveAsync(_selectedPreset, ShellPage.SendDefaultIPCMessage);
                var format = _loader.GetString("Oobe_PresetPicker_AppliedStatus.Text");
                var presetName = _loader.GetString("Preset_" + _selectedPreset.Id + "_Name");
                AppliedInfoBar.Message = string.IsNullOrEmpty(format)
                    ? string.Empty
                    : string.Format(CultureInfo.CurrentCulture, format, changed, presetName);
                AppliedInfoBar.IsOpen = true;
                _applied = true;

                // 应用后锁定选择，避免同一首启会话重复应用造成困惑
                ScenarioSelector.IsEnabled = false;
                SkipButton.IsEnabled = false;
            }
            finally
            {
                ApplyButton.IsEnabled = !_applied;
            }
        }

        private void SkipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // 跳过：不应用任何预设。用户可稍后在 Settings > 推荐配置 中自行启用。
            AppliedInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
            AppliedInfoBar.Message = _loader.GetString("Oobe_PresetPicker_SkipHint.Text");
            AppliedInfoBar.IsOpen = true;
            SkipButton.IsEnabled = false;
            ApplyButton.IsEnabled = false;
        }
    }
}
