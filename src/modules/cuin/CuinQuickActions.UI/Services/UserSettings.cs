// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 读取 CuinQuickActions 模块 settings（%LOCALAPPDATA%\PowerToysCuin\CuinQuickActions\settings.json）。
using System;
using CuinQuickActions.Common;
using Microsoft.PowerToys.Settings.UI.Library;

namespace CuinQuickActions.UI.Services
{
    public sealed class UserSettings : IUserSettings
    {
        private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;

        public bool CloseAfterLosingFocus { get; private set; } = true;

        public UserSettings()
        {
            try
            {
                var settings = _settingsUtils.GetSettingsOrDefault<CuinQuickActionsSettings>(CuinConstants.ModuleName);
                if (settings?.Properties?.CloseAfterLosingFocus != null)
                {
                    CloseAfterLosingFocus = settings.Properties.CloseAfterLosingFocus.Value;
                }
            }
            catch (Exception)
            {
                // settings 损坏时保持默认值（true），面板照常工作。
            }
        }
    }
}
