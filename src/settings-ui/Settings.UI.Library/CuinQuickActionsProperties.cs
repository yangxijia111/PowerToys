// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] CuinQuickActions 的属性体（JSON 属性名与 C++ 壳 dllmain.cpp 的解析 key 对应）。
using System.Text.Json;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CuinQuickActionsProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(false, true, true, false, 0x51); // Ctrl+Alt+Q

        public CuinQuickActionsProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            CloseAfterLosingFocus = new BoolProperty(true);
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public BoolProperty CloseAfterLosingFocus { get; set; }

        public override string ToString() => JsonSerializer.Serialize(this, SettingsSerializationContext.Default.CuinQuickActionsProperties);
    }
}
