// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SettingsUITests
{
    /// <summary>
    /// 场景推荐配置（Presets）服务测试：验证内置场景目录与应用逻辑。
    /// </summary>
    [TestClass]
    public class PresetsServiceTests
    {
        [TestMethod]
        public void Catalog_ShouldContainUniqueNonEmptyPresets()
        {
            var presets = PresetsCatalog.All;

            Assert.IsTrue(presets.Count >= 5, "内置场景不应少于 5 个");
            Assert.AreEqual(presets.Count, presets.Select(p => p.Id).Distinct().Count(), "场景 Id 必须唯一");
            Assert.IsTrue(presets.All(p => p.EnableModules.Count > 0), "每个场景至少推荐启用一个工具");
            Assert.IsTrue(presets.All(p => p.EnableModules.Distinct().Count() == p.EnableModules.Count), "场景内工具不应重复");
        }

        [TestMethod]
        public void GetById_ShouldMatchCaseInsensitive_AndReturnNullForUnknown()
        {
            Assert.IsNotNull(PresetsCatalog.GetById("developer"));
            Assert.IsNotNull(PresetsCatalog.GetById("DEVELOPER"));
            Assert.IsNull(PresetsCatalog.GetById("NoSuchPreset"));
        }

        [TestMethod]
        public void Apply_ShouldEnablePresetModules()
        {
            var config = new GeneralSettings();
            var preset = PresetsCatalog.GetById("Developer");

            PresetsService.Apply(preset, config);

            foreach (var moduleType in preset.EnableModules)
            {
                Assert.IsTrue(ModuleHelper.GetIsModuleEnabled(config, moduleType), $"应用开发场景后 {moduleType} 应被启用");
            }
        }

        [TestMethod]
        public void Apply_ShouldBeIdempotent()
        {
            var config = new GeneralSettings();
            var preset = PresetsCatalog.GetById("Design");

            var firstRunChanges = PresetsService.Apply(preset, config);
            Assert.IsTrue(firstRunChanges > 0, "首次应用应产生变更");

            var secondRunChanges = PresetsService.Apply(preset, config);
            Assert.AreEqual(0, secondRunChanges, "重复应用不应再产生变更");
        }

        [TestMethod]
        public void Apply_ShouldOnlyTouchListedModules()
        {
            var config = new GeneralSettings();
            var preset = PresetsCatalog.GetById("Office");

            // 记录场景未涉及的工具状态
            var untouchedModules = Enum.GetValues(typeof(ModuleType))
                .Cast<ModuleType>()
                .Where(m => !preset.EnableModules.Contains(m) && !preset.DisableModules.Contains(m))
                .Select(m => (m, before: ModuleHelper.GetIsModuleEnabled(config, m)))
                .ToList();

            PresetsService.Apply(preset, config);

            foreach (var (moduleType, before) in untouchedModules)
            {
                Assert.AreEqual(
                    before,
                    ModuleHelper.GetIsModuleEnabled(config, moduleType),
                    $"应用场景不应改变未列出的工具 {moduleType} 的状态");
            }
        }

        [TestMethod]
        public void Apply_ShouldNeverDisableAlreadyEnabledModules()
        {
            // "只开不关"保证：预启用一批与场景无关的工具，应用后必须保持启用
            var config = new GeneralSettings();
            var preset = PresetsCatalog.GetById("Learning");

            var unrelated = Enum.GetValues(typeof(ModuleType))
                .Cast<ModuleType>()
                .Where(m => !preset.EnableModules.Contains(m))
                .ToList();
            foreach (var m in unrelated)
            {
                ModuleHelper.SetIsModuleEnabled(config, m, true);
            }

            PresetsService.Apply(preset, config);

            foreach (var m in unrelated)
            {
                Assert.IsTrue(ModuleHelper.GetIsModuleEnabled(config, m), $"已启用工具 {m} 不应被场景应用关闭");
            }
        }

        [TestMethod]
        public void Apply_NullArguments_ShouldThrow()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                PresetsService.Apply(null, new GeneralSettings()));
            Assert.ThrowsException<ArgumentNullException>(() =>
                PresetsService.Apply(PresetsCatalog.All[0], null));
        }
}
    }
