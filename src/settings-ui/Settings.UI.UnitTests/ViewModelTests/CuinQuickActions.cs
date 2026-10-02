// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] CuinQuickActionsViewModel 测试：启停开关的 IPC 通知、
// 模块设置的 IPC 转发、热键 null 回退默认值。
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public class CuinQuickActions
{
    [TestMethod]
    public void IsEnabled_DefaultsToOff()
    {
        var (viewModel, _, _) = CreateViewModel();
        Assert.IsFalse(viewModel.IsEnabled, "新模块默认关闭");
    }

    [TestMethod]
    public void IsEnabled_SettingToOn_SendsGeneralSettingsIpc()
    {
        var (viewModel, messages, repository) = CreateViewModel();

        viewModel.IsEnabled = true;

        Assert.IsTrue(repository.SettingsConfig.Enabled.CuinQuickActions);
        Assert.AreEqual(1, messages.Count);
        StringAssert.Contains(messages[0], "CuinQuickActions");
    }

    [TestMethod]
    public void IsEnabled_SettingSameValue_DoesNotResend()
    {
        var (viewModel, messages, _) = CreateViewModel();

        viewModel.IsEnabled = false;

        Assert.AreEqual(0, messages.Count, "值未变化不应发送 IPC");
    }

    [TestMethod]
    public void CloseAfterLosingFocus_Change_SendsModuleSettingsIpc()
    {
        var (viewModel, messages, _) = CreateViewModel();

        viewModel.CloseAfterLosingFocus = false;

        Assert.AreEqual(1, messages.Count);
        StringAssert.Contains(messages[0], "CuinQuickActions");
        StringAssert.Contains(messages[0], "CloseAfterLosingFocus");
    }

    [TestMethod]
    public void ActivationShortcut_SettingNull_FallsBackToDefault()
    {
        var (viewModel, messages, _) = CreateViewModel();

        viewModel.ActivationShortcut = null!;

        Assert.AreEqual(0x51, viewModel.ActivationShortcut.Code);
        Assert.AreEqual(1, messages.Count);
    }

    [TestMethod]
    public void RefreshEnabledState_PicksUpExternalChange()
    {
        var (viewModel, _, repository) = CreateViewModel();

        repository.SettingsConfig.Enabled.CuinQuickActions = true;
        viewModel.RefreshEnabledState();

        Assert.IsTrue(viewModel.IsEnabled);
    }

    private static (CuinQuickActionsViewModel ViewModel, List<string> Messages, BackCompatTestProperties.MockSettingsRepository<GeneralSettings> Repository) CreateViewModel()
    {
        var messages = new List<string>();
        var repository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);

        var viewModel = new CuinQuickActionsViewModel(
            ISettingsUtilsMocks.GetStubSettingsUtils<CuinQuickActionsSettings>().Object,
            repository,
            message =>
            {
                messages.Add(message);
                return 0;
            });

        return (viewModel, messages, repository);
    }
}
