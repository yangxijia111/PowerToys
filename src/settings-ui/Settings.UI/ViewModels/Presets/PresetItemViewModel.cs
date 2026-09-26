// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>单个场景卡片的视图模型。</summary>
    public class PresetItemViewModel : Observable
    {
        private bool _isApplied;

        public PresetDefinition Definition { get; }

        public string Id => Definition.Id;

        public string Name => ResourceLoaderInstance.ResourceLoader.GetString($"Preset_{Definition.Id}_Name");

        public int ModuleCount => Definition.EnableModules.Count;

        /// <summary>“包含 N 个工具”的本地化文案。</summary>
        public string ModuleCountText
        {
            get
            {
                var format = ResourceLoaderInstance.ResourceLoader.GetString("Presets_ModuleCountFormat");
                return string.IsNullOrEmpty(format) ? ModuleCount.ToString(CultureInfo.CurrentCulture) : string.Format(CultureInfo.CurrentCulture, format, ModuleCount);
            }
        }

        public string Description => ResourceLoaderInstance.ResourceLoader.GetString($"Preset_{Definition.Id}_Description") + "\n" + ModuleCountText;

        public ICommand ApplyCommand { get; }

        public bool IsApplied
        {
            get => _isApplied;
            private set => Set(ref _isApplied, value);
        }

        public PresetItemViewModel(PresetDefinition definition, Action<PresetItemViewModel> applyAction)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            ApplyCommand = new RelayCommand(() => applyAction?.Invoke(this));
        }

        public void MarkApplied()
        {
            IsApplied = true;
        }
    }
}
