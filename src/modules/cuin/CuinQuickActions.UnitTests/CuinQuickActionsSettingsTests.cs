// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] CuinQuickActionsSettings 持久化测试：默认值、序列化 roundtrip、
// 残缺 JSON 韧性（绝不因配置损坏抛出导致进程崩溃）。
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CuinQuickActions.UnitTests
{
    [TestClass]
    public class CuinQuickActionsSettingsTests
    {
        [TestMethod]
        public void Defaults_AreCorrect()
        {
            var settings = new CuinQuickActionsSettings();

            Assert.AreEqual("CuinQuickActions", settings.Name);
            Assert.AreEqual("CuinQuickActions", settings.GetModuleName());
            Assert.AreEqual("1.0.0", settings.Version);

            // 默认热键：Ctrl+Alt+Q
            Assert.IsFalse(settings.Properties.ActivationShortcut.Win);
            Assert.IsTrue(settings.Properties.ActivationShortcut.Ctrl);
            Assert.IsTrue(settings.Properties.ActivationShortcut.Alt);
            Assert.IsFalse(settings.Properties.ActivationShortcut.Shift);
            Assert.AreEqual(0x51, settings.Properties.ActivationShortcut.Code);

            // 默认失焦关闭
            Assert.IsTrue(settings.Properties.CloseAfterLosingFocus.Value);
        }

        [TestMethod]
        public void EnabledModules_DefaultsToOff()
        {
            var enabled = new EnabledModules();
            Assert.IsFalse(enabled.CuinQuickActions, "新增常驻进程模块默认关闭（用户显式开启）");
        }

        [TestMethod]
        public void Serialization_RoundTrips()
        {
            var settings = new CuinQuickActionsSettings();
            settings.Properties.CloseAfterLosingFocus.Value = false;
            settings.Properties.ActivationShortcut = new HotkeySettings(true, false, false, false, 0x42);

            var json = settings.ToJsonString();
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var restored = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.CuinQuickActionsSettings);
            Assert.IsNotNull(restored);
            Assert.AreEqual(settings.Name, restored.Name);
            Assert.AreEqual(settings.Version, restored.Version);
            Assert.IsFalse(restored.Properties.CloseAfterLosingFocus.Value);
            Assert.IsTrue(restored.Properties.ActivationShortcut.Win);
            Assert.AreEqual(0x42, restored.Properties.ActivationShortcut.Code);
        }

        [TestMethod]
        public void Json_ContainsContractKeysExpectedByNativeModule()
        {
            // C++ 壳（dllmain.cpp）按 "properties" / "ActivationShortcut" 读取，
            // 这里锁定 JSON 合约，防止 C# 侧重命名后静默失联。
            var json = new CuinQuickActionsSettings().ToJsonString();
            StringAssert.Contains(json, "\"properties\"");
            StringAssert.Contains(json, "\"ActivationShortcut\"");
            StringAssert.Contains(json, "\"win\"");
            StringAssert.Contains(json, "\"code\"");
        }

        [TestMethod]
        public void MalformedJson_DoesNotThrowDuringDeserializeAttempt()
        {
            // 与 SettingsUtils.GetSettingsOrDefault 的兜底配合：反序列化失败允许抛出，
            // 但必须能被上层捕获；这里验证合法但残缺的 JSON（缺 properties）反序列化不抛，
            // 且缺省字段回落到构造默认值（关闭失焦=true、热键=Ctrl+Alt+Q），
            // 面板进程不会因配置残缺而崩溃或失去默认行为。
            var json = "{\"name\":\"CuinQuickActions\",\"version\":\"1.0.0\"}";
            var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.CuinQuickActionsSettings);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.Properties);
            Assert.IsTrue(settings.Properties.CloseAfterLosingFocus.Value);
            Assert.AreEqual(0x51, settings.Properties.ActivationShortcut.Code);
        }

        [TestMethod]
        public void UpgradeSettingsConfiguration_FirstVersion_ReturnsFalse()
        {
            Assert.IsFalse(new CuinQuickActionsSettings().UpgradeSettingsConfiguration());
        }

        [TestMethod]
        public void HotkeyAccessors_ExposeActivationShortcut()
        {
            var accessors = new CuinQuickActionsSettings().GetAllHotkeyAccessors();
            Assert.AreEqual(1, accessors.Length);
            Assert.AreEqual("Activation_Shortcut", accessors[0].LocalizationHeaderKey);
            Assert.AreEqual(0x51, accessors[0].Value.Code);
        }
    }
}
