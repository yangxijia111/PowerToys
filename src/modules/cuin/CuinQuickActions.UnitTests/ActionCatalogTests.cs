// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] ActionCatalog 完整性测试：ID 唯一、resw key 命名、危险标记、
// URI/ShellTarget 白名单、分组覆盖。
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CuinQuickActions.Common;
using CuinQuickActions.Common.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CuinQuickActions.UnitTests
{
    [TestClass]
    public class ActionCatalogTests
    {
        // 目录允许启动的固定可执行文件白名单（新增动作必须同步更新此测试）。
        private static readonly HashSet<string> AllowedExecutables = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "taskmgr.exe",
            "rundll32.exe",
            "notepad.exe",
            "explorer.exe",
        };

        [TestMethod]
        public void Catalog_IsNotEmpty_And_IdsAreUnique()
        {
            Assert.IsTrue(ActionCatalog.All.Count >= 10, "目录应至少包含 10 个动作");
            var ids = ActionCatalog.All.Select(a => a.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        [TestMethod]
        public void Catalog_IdsFollowSnakeCaseConvention()
        {
            var regex = new Regex("^[a-z][a-z0-9_]*$");
            foreach (var action in ActionCatalog.All)
            {
                Assert.IsTrue(regex.IsMatch(action.Id), $"ID '{action.Id}' 不符合 snake_case 约定");
            }
        }

        [TestMethod]
        public void Catalog_ResourceKeysFollowConvention()
        {
            foreach (var action in ActionCatalog.All)
            {
                Assert.AreEqual($"QuickAction_{action.Id}_Title", action.TitleResourceKey);
                Assert.AreEqual($"QuickAction_{action.Id}_Description", action.DescriptionResourceKey);
            }
        }

        [TestMethod]
        public void Catalog_DestructiveActionsAreExactlyCloseAppAndRestartExplorer()
        {
            var destructive = ActionCatalog.All.Where(a => a.IsDestructive).Select(a => a.Id).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(new List<string> { "close_foreground_app", "restart_explorer" }, destructive);
        }

        [TestMethod]
        public void Catalog_LaunchUriActionsUseOnlyMsSettingsScheme()
        {
            var uriActions = ActionCatalog.All.Where(a => a.Kind == QuickActionKind.LaunchUri);
            Assert.IsTrue(uriActions.Any());
            foreach (var action in uriActions)
            {
                Assert.IsNotNull(action.Uri, $"动作 '{action.Id}' 缺少 Uri");
                Assert.IsTrue(
                    action.Uri.StartsWith("ms-settings:", System.StringComparison.OrdinalIgnoreCase),
                    $"动作 '{action.Id}' 的 URI 不在 ms-settings: 方案内：{action.Uri}");
            }
        }

        [TestMethod]
        public void Catalog_ShellTargetsAreWhitelisted_WithNoUserInput()
        {
            var shellActions = ActionCatalog.All.Where(a => a.Kind == QuickActionKind.LaunchShellTarget);
            Assert.IsTrue(shellActions.Any());
            foreach (var action in shellActions)
            {
                Assert.IsNotNull(action.ShellTarget, $"动作 '{action.Id}' 缺少 ShellTarget");
                Assert.IsTrue(
                    AllowedExecutables.Contains(action.ShellTarget.Executable),
                    $"动作 '{action.Id}' 的可执行文件 '{action.ShellTarget.Executable}' 不在白名单内");
            }
        }

        [TestMethod]
        public void Catalog_NoActionRequiresElevation()
        {
            foreach (var action in ActionCatalog.All)
            {
                Assert.IsFalse(action.RequiresElevation, $"动作 '{action.Id}' 不应要求提权（当前版本约束）");
            }
        }

        [TestMethod]
        public void Catalog_EveryGroupHasAtLeastOneAction()
        {
            foreach (QuickActionGroup group in System.Enum.GetValues(typeof(QuickActionGroup)))
            {
                Assert.IsTrue(ActionCatalog.GetByGroup(group).Any(), $"分组 {group} 不应为空");
            }
        }

        [TestMethod]
        public void Catalog_EveryActionHasGlyph()
        {
            foreach (var action in ActionCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(action.Glyph), $"动作 '{action.Id}' 缺少图标字形");
            }
        }

        [TestMethod]
        public void FindById_ReturnsKnownAction_And_NullForUnknown()
        {
            var action = ActionCatalog.FindById("open_task_manager");
            Assert.IsNotNull(action);
            Assert.AreEqual(QuickActionKind.LaunchShellTarget, action.Kind);

            Assert.IsNull(ActionCatalog.FindById("nonexistent_action"));
            Assert.IsNull(ActionCatalog.FindById(string.Empty));
        }

        [TestMethod]
        public void GetByGroup_ReturnsOnlyThatGroup()
        {
            var system = ActionCatalog.GetByGroup(QuickActionGroup.System);
            Assert.IsTrue(system.All(a => a.Group == QuickActionGroup.System));
        }

        [TestMethod]
        public void Constants_EventNamesMatchSharedConstantsContract()
        {
            // 与 shared_constants.h 的 C++ 字面量一致性由 tools/check_cuin_modules.py 静态校验；
            // 这里锁定 C# 侧事件名格式，防止意外改动。
            Assert.IsTrue(
                CuinConstants.ShowEventName.StartsWith(@"Local\PowerToysCuin-QuickActions-ShowEvent-", System.StringComparison.Ordinal),
                CuinConstants.ShowEventName);
            Assert.IsTrue(
                CuinConstants.TerminateEventName.StartsWith(@"Local\PowerToysCuin-QuickActions-TerminateEvent-", System.StringComparison.Ordinal),
                CuinConstants.TerminateEventName);
            Assert.AreEqual("CuinQuickActions", CuinConstants.ModuleName);
        }
    }
}
