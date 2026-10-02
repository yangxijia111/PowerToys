// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] Cuin Quick Actions 面板应用入口（结构抄自 Peek.UI App）。
// 生命周期：runner（经 C++ 壳）启动本进程并传 runner PID → 监听 Show/Terminate 命名事件。
using System;
using System.Threading;
using CuinQuickActions.Common;
using CuinQuickActions.Common.Actions;
using CuinQuickActions.UI.Services;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace CuinQuickActions.UI
{
    public partial class App : Application
    {
        public static int PowerToysPID { get; set; }

        private MainWindow? Window { get; set; }

        // 面板呼出前的前台窗口：close_foreground_app 动作的目标。
        // 必须在面板激活前记录（面板自身会抢占前台）。
        private IntPtr _lastForegroundWindow;

        private IQuickActionExecutor Executor { get; set; } = null!;

        public App()
        {
            var appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            InitializeComponent();
            Logger.InitializeLogger("\\CuinQuickActions\\Logs");

            UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Executor = new WindowsQuickActionExecutor(() => _lastForegroundWindow);

            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1 && int.TryParse(cmdArgs[^1], out int powerToysRunnerPid))
            {
                RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () => Environment.Exit(0));
            }
            else
            {
                // 未经 runner 启动（例如手动调试）：直接退出，保持常驻进程只由 runner 管理。
                Logger.LogWarning("Cuin Quick Actions started without a PowerToys runner PID. Exiting.");
                Environment.Exit(0);
                return;
            }

            NativeEventWaiter.WaitForEventLoop(CuinConstants.ShowEventName, OnShowQuickActions);
            NativeEventWaiter.WaitForEventLoop(CuinConstants.TerminateEventName, () => Environment.Exit(0));
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception in Cuin Quick Actions", e.Exception);
            e.Handled = true;
        }

        /// <summary>
        /// 处理热键：记录当前前台窗口（此时面板尚未激活），然后显示面板。
        /// </summary>
        private void OnShowQuickActions()
        {
            _lastForegroundWindow = Windows.Win32.PInvoke_CuinQuickActions.GetForegroundWindow();

            if (Window == null)
            {
                Window = new MainWindow(Executor, () => _lastForegroundWindow);
            }

            Window.ShowPanel();
        }

        /// <summary>面板窗口关闭后由其回调，允许下次热键重建窗口。</summary>
        internal void NotifyWindowClosed()
        {
            Window = null;
        }
    }
}
