// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    /// <summary>
    /// [fork-identity] Fork 发行身份校验（Phase 3）。
    /// 与 tools/check_fork_identity.py、docs/IDENTITY_MAP.md 共同维护；修改身份常量时三处必须同步。
    /// </summary>
    [TestClass]
    public class ForkIdentityTests
    {
        [TestMethod]
        public void ForkAppDataFolderNameMustBeIsolatedFromOfficial()
        {
            // 官方使用 "Microsoft\PowerToys"；fork 必须使用独立根，防止设置/日志互相污染。
            Assert.AreEqual("PowerToysCuin", Branding.ForkAppDataFolderName);
        }

        [TestMethod]
        public void ForkAppDataFolderNameMustNotContainOfficialSubpath()
        {
            Assert.IsFalse(Branding.ForkAppDataFolderName.Contains("Microsoft"));
            Assert.IsFalse(Branding.ForkAppDataFolderName.Contains(Path.Combine("Microsoft", "PowerToys")));
        }

        [TestMethod]
        public void SettingPathMustResolveUnderForkAppDataRoot()
        {
            var settingPath = new SettingPath();

            var generalPath = settingPath.GetSettingsPath(string.Empty);
            StringAssert.Contains(generalPath, Branding.ForkAppDataFolderName);
            Assert.IsFalse(generalPath.Contains(Path.Combine("Microsoft", "PowerToys")));

            var modulePath = settingPath.GetSettingsPath("PowerRename");
            StringAssert.Contains(modulePath, Branding.ForkAppDataFolderName);
            StringAssert.Contains(modulePath, "PowerRename");
        }

        [TestMethod]
        public void BrandingForkNameMustStayConsistent()
        {
            // 安装器 Product.wxs / Core.wxs 与 C++ 托盘文案与本常量人工同步（[fork-brand] 锚点）。
            Assert.AreEqual("PowerToys Cuin", Branding.ForkName);
            Assert.AreEqual("Microsoft PowerToys", Branding.UpstreamName);
        }
    }
}
