// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 前台进程保护名单测试：系统关键进程/自身进程拒绝终止。
using CuinQuickActions.Common.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CuinQuickActions.UnitTests
{
    [TestClass]
    public class ForegroundProcessGuardTests
    {
        [DataTestMethod]
        [DataRow("winlogon.exe")]
        [DataRow("csrss.exe")]
        [DataRow("services.exe")]
        [DataRow("lsass.exe")]
        [DataRow("smss.exe")]
        [DataRow("wininit.exe")]
        [DataRow("svchost.exe")]
        [DataRow("dwm.exe")]
        [DataRow("explorer.exe")]
        [DataRow("System")]
        [DataRow("MemCompression")]
        [DataRow("runtimebroker.exe")]
        [DataRow("ctfmon.exe")]
        public void SystemProcesses_AreProtected(string processName)
        {
            Assert.IsTrue(ForegroundProcessGuard.IsProtectedProcess(processName), $"{processName} 应受保护");
        }

        [DataTestMethod]
        [DataRow("powertoys.cuinquickactions.ui.exe")]
        [DataRow("powertoys.exe")]
        public void SelfProcesses_AreProtected(string processName)
        {
            Assert.IsTrue(ForegroundProcessGuard.IsProtectedProcess(processName));
        }

        [DataTestMethod]
        [DataRow("notepad.exe")]
        [DataRow("winword.exe")]
        [DataRow("chrome.exe")]
        [DataRow("somegame")]
        public void RegularApps_AreNotProtected(string processName)
        {
            Assert.IsFalse(ForegroundProcessGuard.IsProtectedProcess(processName), $"{processName} 不应被误保护");
        }

        [TestMethod]
        public void Matching_IsCaseInsensitive_AndExeSuffixOptional()
        {
            Assert.IsTrue(ForegroundProcessGuard.IsProtectedProcess("EXPLORER.EXE"));
            Assert.IsTrue(ForegroundProcessGuard.IsProtectedProcess("explorer"));
            Assert.IsTrue(ForegroundProcessGuard.IsProtectedProcess("CsrSs"));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        public void UnknownOrEmpty_IsTreatedAsProtected(string? processName)
        {
            Assert.IsTrue(ForegroundProcessGuard.IsProtectedProcess(processName!));
        }
    }
}
