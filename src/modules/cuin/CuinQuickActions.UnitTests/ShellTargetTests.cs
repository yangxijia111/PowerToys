// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] ShellTarget 参数展开测试（结构化参数，仅 %ENV% 展开，无 shell 注入面）。
using CuinQuickActions.Common.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CuinQuickActions.UnitTests
{
    [TestClass]
    public class ShellTargetTests
    {
        [TestMethod]
        public void ExpandArguments_NullArguments_ReturnsEmpty()
        {
            var target = new ShellTarget { Executable = "taskmgr.exe", Arguments = null };
            Assert.AreEqual(string.Empty, target.ExpandArguments());
        }

        [TestMethod]
        public void ExpandArguments_ExpandsEnvironmentVariables()
        {
            var target = new ShellTarget { Executable = "notepad.exe", Arguments = @"%TEMP%\notes.txt" };
            var expanded = target.ExpandArguments();
            StringAssert.StartsWith(expanded, System.Environment.GetEnvironmentVariable("TEMP"));
            Assert.IsFalse(expanded.Contains("%TEMP%"), "环境变量应被完全展开");
        }

        [TestMethod]
        public void ExpandArguments_PlainArguments_PassThroughUnchanged()
        {
            var target = new ShellTarget { Executable = "rundll32.exe", Arguments = "sysdm.cpl,EditEnvironmentVariables" };
            Assert.AreEqual("sysdm.cpl,EditEnvironmentVariables", target.ExpandArguments());
        }
    }
}
