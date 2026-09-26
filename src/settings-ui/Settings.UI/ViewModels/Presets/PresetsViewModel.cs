// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// 场景推荐配置页视图模型：展示内置场景并支持一键应用。
    /// </summary>
    public class PresetsViewModel : Observable
    {
        private string _statusMessage;

        private bool _isApplying;

        public ObservableCollection<PresetItemViewModel> Presets { get; } = new ObservableCollection<PresetItemViewModel>();

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public PresetsViewModel()
        {
            foreach (var preset in PresetsCatalog.All)
            {
                Presets.Add(new PresetItemViewModel(preset, ApplyPreset));
            }
        }

        private async void ApplyPreset(PresetItemViewModel preset)
        {
            if (preset == null || _isApplying)
            {
                return;
            }

            _isApplying = true;
            try
            {
                var changed = await PresetsService.ApplyAndSaveAsync(preset.Definition, ShellPage.SendDefaultIPCMessage);
                var format = ResourceLoaderInstance.ResourceLoader.GetString("Presets_AppliedStatus");
                StatusMessage = string.IsNullOrEmpty(format) ? string.Empty : string.Format(CultureInfo.CurrentCulture, format, preset.Name, changed);
                preset.MarkApplied();
            }
            finally
            {
                _isApplying = false;
            }
        }
    }
}
