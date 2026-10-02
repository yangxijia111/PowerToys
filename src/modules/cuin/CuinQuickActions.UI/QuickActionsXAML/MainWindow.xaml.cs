// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] 面板窗口行为：居中显示、Esc 关闭、失焦自动关闭（可配置）、
// 危险操作确认对话框、执行结果反馈。
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CuinQuickActions.Common.Actions;
using CuinQuickActions.UI.Services;
using CuinQuickActions.UI.ViewModels;
using ManagedCommon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.Resources;
using Windows.Graphics;

namespace CuinQuickActions.UI
{
    public sealed partial class MainWindow : Window
    {
        private const int PanelWidthDip = 520;
        private const int PanelHeightDip = 640;

        private readonly MainWindowViewModel _viewModel;
        private readonly IUserSettings _userSettings;
        private readonly Func<IntPtr> _lastForegroundWindowProvider;
        private readonly ResourceLoader _loader = new();

        public MainWindow(IQuickActionExecutor executor, Func<IntPtr> lastForegroundWindowProvider)
        {
            _userSettings = new UserSettings();
            _lastForegroundWindowProvider = lastForegroundWindowProvider ?? throw new ArgumentNullException(nameof(lastForegroundWindowProvider));
            _viewModel = new MainWindowViewModel(new QuickActionRunner(executor));

            InitializeComponent();
            Title = _loader.GetString("Panel_Title");

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            RootGrid.DataContext = _viewModel;

            Activated += OnActivated;
            Closed += (sender, args) => (App.Current as App)?.NotifyWindowClosed();

            var escAccelerator = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
            escAccelerator.Invoked += (sender, args) => Close();
            RootGrid.KeyboardAccelerators.Add(escAccelerator);
        }

        /// <summary>显示并居中面板（每次热键呼出时调用）。</summary>
        public void ShowPanel()
        {
            Activate();
            CenterOnScreen();
        }

        internal void CenterOnScreen()
        {
            if (Content?.XamlRoot is null)
            {
                return;
            }

            var scale = Content.XamlRoot.RasterizationScale;
            var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var width = (int)(PanelWidthDip * scale);
            var height = (int)(PanelHeightDip * scale);
            AppWindow.MoveAndResize(new RectInt32(
                workArea.X + ((workArea.Width - width) / 2),
                workArea.Y + (int)((workArea.Height - height) * 0.35),
                width,
                height));
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && _userSettings.CloseAfterLosingFocus)
            {
                Close();
            }
        }

        private async void OnActionClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: QuickActionItemViewModel item })
            {
                await InvokeActionAsync(item);
            }
        }

        private async Task InvokeActionAsync(QuickActionItemViewModel item)
        {
            var userConfirmed = true;
            if (MainWindowViewModel.NeedsConfirmation(item))
            {
                userConfirmed = await ShowConfirmDialogAsync(item);
                if (!userConfirmed)
                {
                    return;
                }
            }

            var result = await _viewModel.ExecuteAsync(item, userConfirmed);

            if (result.Status == QuickActionExecutionStatus.Success)
            {
                Close();
            }
            else if (result.Status != QuickActionExecutionStatus.RejectedNeedsConfirmation)
            {
                await ShowErrorDialogAsync(item);
            }
        }

        private async Task<bool> ShowConfirmDialogAsync(QuickActionItemViewModel item)
        {
            var content = _loader.GetString(item.Definition.DescriptionResourceKey);
            if (item.Definition.Kind == QuickActionKind.CloseForegroundApplication)
            {
                content += Environment.NewLine + string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    _loader.GetString("Dialog_ConfirmTargetFormat"),
                    GetForegroundProcessDisplayName());
            }

            var dialog = new ContentDialog
            {
                Title = _loader.GetString("Dialog_ConfirmTitle"),
                Content = content,
                PrimaryButtonText = _loader.GetString("Dialog_ConfirmButton"),
                CloseButtonText = _loader.GetString("Dialog_CancelButton"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private async Task ShowErrorDialogAsync(QuickActionItemViewModel item)
        {
            Logger.LogWarning($"Quick action '{item.Definition.Id}' failed.");

            var dialog = new ContentDialog
            {
                Title = _loader.GetString("Dialog_ErrorTitle"),
                Content = _loader.GetString("Dialog_ErrorContent"),
                CloseButtonText = _loader.GetString("Dialog_CancelButton"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };

            await dialog.ShowAsync();
        }

        private string GetForegroundProcessDisplayName()
        {
            try
            {
                var hwnd = _lastForegroundWindowProvider();
                if (hwnd == IntPtr.Zero)
                {
                    return _loader.GetString("Dialog_UnknownTarget");
                }

                Windows.Win32.PInvoke_CuinQuickActions.GetWindowThreadProcessId(
                    (Windows.Win32.Foundation.HWND)hwnd, out uint pid);
                if (pid == 0)
                {
                    return _loader.GetString("Dialog_UnknownTarget");
                }

                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch (Exception)
            {
                return _loader.GetString("Dialog_UnknownTarget");
            }
        }
    }
}
