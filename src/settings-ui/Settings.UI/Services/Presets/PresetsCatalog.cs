// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// 内置场景目录：面向不同使用人群的推荐配置。
    /// </summary>
    public static class PresetsCatalog
    {
        public static IReadOnlyList<PresetDefinition> All { get; } = new List<PresetDefinition>
        {
            new PresetDefinition(
                "General",
                new[]
                {
                    ModuleType.PowerLauncher, ModuleType.ColorPicker, ModuleType.PowerOCR,
                    ModuleType.PowerRename, ModuleType.AlwaysOnTop, ModuleType.Awake,
                }),
            new PresetDefinition(
                "Developer",
                new[]
                {
                    ModuleType.FancyZones, ModuleType.PowerLauncher, ModuleType.AdvancedPaste,
                    ModuleType.PowerOCR, ModuleType.FileLocksmith, ModuleType.EnvironmentVariables,
                    ModuleType.RegistryPreview, ModuleType.Awake,
                }),
            new PresetDefinition(
                "Learning",
                new[]
                {
                    ModuleType.FancyZones, ModuleType.PowerLauncher, ModuleType.ShortcutGuide,
                    ModuleType.PowerOCR, ModuleType.ZoomIt, ModuleType.AlwaysOnTop, ModuleType.Peek,
                }),
            new PresetDefinition(
                "Office",
                new[]
                {
                    ModuleType.PowerLauncher, ModuleType.ImageResizer, ModuleType.PowerRename,
                    ModuleType.AlwaysOnTop, ModuleType.Peek, ModuleType.FileLocksmith,
                    ModuleType.Awake, ModuleType.PowerAccent,
                }),
            new PresetDefinition(
                "Design",
                new[]
                {
                    ModuleType.ColorPicker, ModuleType.MeasureTool, ModuleType.FancyZones,
                    ModuleType.ZoomIt, ModuleType.PowerOCR, ModuleType.PowerLauncher,
                }),
        };

        public static PresetDefinition GetById(string id)
        {
            return All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
