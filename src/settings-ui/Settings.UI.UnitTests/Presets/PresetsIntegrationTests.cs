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
    /// Presets 端到端补充：IPC 消息往返、目录定义有效性、重复应用一致性。
    /// </summary>
    [TestClass]
    public class PresetsIntegrationTests
    {
        [TestMethod]
        public void Apply_IpcMessageRoundTrip_ShouldPreserveEnabledStates()
        {
            // 验证链路"Settings 写入 → IPC 出站消息"的承载一致性：
            // Apply 后经 OutGoingGeneralSettings 序列化（即 SendConfigMSG 实际发送的 JSON），
            // runner 侧按相同结构反序列化后必须读到一致的启用状态。
            var config = new GeneralSettings();
            var preset = PresetsCatalog.GetById("Developer");
            PresetsService.Apply(preset, config);

            var ipcJson = new OutGoingGeneralSettings(config).ToString();
            var parsed = JsonSerializer.Deserialize<OutGoingGeneralSettings>(ipcJson);

            Assert.IsNotNull(parsed?.GeneralSettings);
            foreach (var moduleType in preset.EnableModules)
            {
                Assert.IsTrue(
                    ModuleHelper.GetIsModuleEnabled(parsed.GeneralSettings, moduleType),
                    $"IPC 往返后 {moduleType} 的启用状态丢失");
            }
        }

        [TestMethod]
        public void Catalog_EveryPresetModule_ShouldBeRecognizedByModuleHelper()
        {
            // 场景中引用的每个 ModuleType 都必须能被 ModuleHelper 读/写，
            // 否则会被静默忽略（推荐工具实际未启用）。
            foreach (var preset in PresetsCatalog.All)
            {
                var config = new GeneralSettings();
                PresetsService.Apply(preset, config);
                foreach (var moduleType in preset.EnableModules)
                {
                    Assert.IsTrue(
                        ModuleHelper.GetIsModuleEnabled(config, moduleType),
                        $"{preset.Id} 场景引用的 {moduleType} 未生效：ModuleHelper 不认识该枚举成员");
                }
            }
        }

        [TestMethod]
        public void Apply_RepeatedApplication_ProducesConsistentState()
        {
            // 重复应用多次后，配置状态与首次应用完全一致（幂等 + 无漂移）
            var first = new GeneralSettings();
            var preset = PresetsCatalog.GetById("General");
            PresetsService.Apply(preset, first);
            var firstJson = new OutGoingGeneralSettings(first).ToString();

            var second = new GeneralSettings();
            PresetsService.Apply(preset, second);
            PresetsService.Apply(preset, second);
            var secondJson = new OutGoingGeneralSettings(second).ToString();

            Assert.AreEqual(firstJson, secondJson, "重复应用后的配置 JSON 应完全一致");
        }
    }
}
