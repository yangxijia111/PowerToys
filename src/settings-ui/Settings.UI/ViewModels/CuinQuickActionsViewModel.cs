// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] Cuin Quick Actions 的 Settings 页 ViewModel（结构抄自 PeekViewModel，
// 简化：无 GPO 管控、无文件 watcher —— 面板进程不回写 settings）。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class CuinQuickActionsViewModel : PageViewModelBase
    {
        protected override string ModuleName => CuinQuickActionsSettings.ModuleName;

        private bool _isEnabled;

        private bool _disposed;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private CuinQuickActionsSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public CuinQuickActionsViewModel(
            SettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> settingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            Settings = (settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils)))
                .GetSettingsOrDefault<CuinQuickActionsSettings>(CuinQuickActionsSettings.ModuleName);

            _isEnabled = GeneralSettingsConfig.Enabled.CuinQuickActions;

            SendConfigMSG = ipcMSGCallBackFunc;
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [ActivationShortcut],
            };

            return hotkeysDict;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;

                    GeneralSettingsConfig.Enabled.CuinQuickActions = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public HotkeySettings ActivationShortcut
        {
            get => Settings.Properties.ActivationShortcut;
            set
            {
                if (Settings.Properties.ActivationShortcut != value)
                {
                    Settings.Properties.ActivationShortcut = value ?? Settings.Properties.DefaultActivationShortcut;
                    OnPropertyChanged(nameof(ActivationShortcut));
                    NotifySettingsChanged();
                }
            }
        }

        public bool CloseAfterLosingFocus
        {
            get => Settings.Properties.CloseAfterLosingFocus.Value;
            set
            {
                if (Settings.Properties.CloseAfterLosingFocus.Value != value)
                {
                    Settings.Properties.CloseAfterLosingFocus.Value = value;
                    OnPropertyChanged(nameof(CloseAfterLosingFocus));
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            // 该 IPC 消息由 runner 截获并转发给 C++ 壳的 set_config() 落盘；
            // 壳在热键变化后会通过 get_hotkeys() 让 runner 重新注册。
            SendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    CuinQuickActionsSettings.ModuleName,
                    JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.CuinQuickActionsSettings)));
        }

        public void RefreshEnabledState()
        {
            _isEnabled = GeneralSettingsConfig.Enabled.CuinQuickActions;
            OnPropertyChanged(nameof(IsEnabled));
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
