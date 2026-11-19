using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using v2rayN.Manager;

namespace v2rayN.Views;

public partial class MainWindow
{
    private readonly Config _config;
    private bool _exiting;
    private BackupAndRestoreView? _backupView;

    public MainWindow()
    {
        _config = AppManager.Instance.Config;
        InitializeComponent();
        RefreshVpnScope();
        if (App.ProgramStarted != null)
        {
            ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted,
                (_, _) => Dispatcher.Invoke(() => ShowHideWindow(true)), null, -1, false);
        }
        Closing += MainWindow_Closing;
        App.Current.SessionEnding += Current_SessionEnding;
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        this.WhenActivated(disposables =>
        {
            ViewModel.ReadTextFromClipboardInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(WindowsUtils.GetClipboardData())).DisposeWith(disposables);
            ViewModel.ShowHideWindowInteraction.RegisterHandler(interaction =>
            {
                ShowHideWindow(interaction.Input);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
            ViewModel.ScanScreenInteraction.RegisterHandler(interaction =>
            {
                ShowHideWindow(false);
                var bytes = QRCodeWindowsUtils.CaptureScreen(this);
                ShowHideWindow(true);
                interaction.SetOutput(bytes);
            }).DisposeWith(disposables);
            ViewModel.BrowseImageFileInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(UI.OpenFileDialog(out var path, "PNG|*.png|All|*.*") == true ? path : null))
                .DisposeWith(disposables);
            ViewModel.ProfilesViewModel.ProfilesFocusInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(RxVoid.Default)).DisposeWith(disposables);
            ViewModel.ProfilesViewModel.DispatcherRefreshServersBizInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(RxVoid.Default)).DisposeWith(disposables);
            ViewModel.ProfilesViewModel.AdjustMainLvColWidthInteraction.RegisterHandler(interaction =>
                interaction.SetOutput(RxVoid.Default)).DisposeWith(disposables);
            ViewModel.StatusBarViewModel.SetClipboardDataInteraction.RegisterHandler(interaction =>
            {
                WindowsUtils.SetClipboardData(interaction.Input);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
            ViewModel.StatusBarViewModel.DispatcherRefreshIconInteraction.RegisterHandler(interaction =>
            {
                tbNotify.ToolTipText = "دارفانت";
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);

            this.WhenAnyValue(view => view.ViewModel.StatusBarViewModel.EnableTun,
                    view => view.ViewModel.BlReloadEnabled)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RefreshConnectionState()).DisposeWith(disposables);
            this.WhenAnyValue(view => view.ViewModel.ProfilesViewModel.SelectedProfile)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => RefreshConnectionState()).DisposeWith(disposables);

            AppEvents.SendSnackMsgRequested.AsObservable().ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(message => MainSnackbar.MessageQueue?.Enqueue(message)).DisposeWith(disposables);
            AppEvents.SendMsgViewRequested.AsObservable().ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(AppendConnectionLog).DisposeWith(disposables);
            AppEvents.AppExitRequested.AsObservable().ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => StorageUI()).DisposeWith(disposables);
            AppEvents.ShutdownRequested.AsObservable().ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    _exiting = true;
                    tbNotify.Dispose();
                    Application.Current.Shutdown();
                }).DisposeWith(disposables);
        });

        if (!_config.GuiItem.EnableHWA)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
        WindowsManager.Instance.RegisterGlobalHotkey(_config, OnHotkeyHandler, null);
    }

    private void RefreshConnectionState()
    {
        if (ViewModel is not { } model)
        {
            return;
        }
        var enabled = model.StatusBarViewModel.EnableTun;
        var busy = !model.BlReloadEnabled;
        togDarfantVpn.IsEnabled = !busy;
        ConnectionTitle.Text = busy && enabled ? "در حال اتصال…" : enabled ? "وی‌پی‌ان روشن است" : "آمادهٔ اتصال";
        ConnectionTitle.Foreground = (Brush)FindResource(enabled ? "DfAccent" : "DfInk");
        ConnectionDetail.Text = model.ProfilesViewModel.SelectedProfile?.Remarks is { Length: > 0 } name
            ? name : "یک سرور انتخاب کنید و متصل شوید";
        tbNotify.ToolTipText = enabled ? "دارفانت • وی‌پی‌ان روشن" : "دارفانت";
    }

    private void AppendConnectionLog(string message)
    {
        if (ConnectionLog.Text.Length > 40000)
        {
            ConnectionLog.Text = ConnectionLog.Text[^20000..];
        }
        ConnectionLog.AppendText(message.TrimEnd() + Environment.NewLine);
        if (LogPanel.IsVisible)
        {
            ConnectionLog.ScrollToEnd();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_exiting)
        {
            e.Cancel = true;
            StorageUI();
            ShowHideWindow(false);
        }
    }

    private async void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        StorageUI();
        await AppManager.Instance.AppExitAsync(false);
    }

    private void OnHotkeyHandler(EGlobalHotkey hotkey)
    {
        if (hotkey == EGlobalHotkey.ShowForm)
        {
            Dispatcher.Invoke(() => ShowHideWindow(null));
        }
        else if (hotkey is EGlobalHotkey.SystemProxyClear or EGlobalHotkey.SystemProxySet
                 or EGlobalHotkey.SystemProxyUnchanged or EGlobalHotkey.SystemProxyPac)
        {
            AppEvents.SysProxyChangeRequested.Publish((ESysProxyType)((int)hotkey - 1));
        }
    }

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            LogPanel.Visibility = Visibility.Collapsed;
            ViewModel?.ProfilesViewModel.ServerSpeedtestStop();
        }
        if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.FocusedElement is not TextBox)
        {
            await AddServerViaClipboardAsync();
            e.Handled = true;
        }
    }

    public async Task AddServerViaClipboardAsync()
    {
        if (ViewModel is { } model && WindowsUtils.GetClipboardData() is { Length: > 0 } clipboard)
        {
            await model.AddServerViaClipboardAsync(clipboard);
        }
    }

    public void ShowHideWindow(bool? show)
    {
        var visible = show ?? !AppManager.Instance.ShowInTaskbar;
        if (visible)
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
        }
        else
        {
            Hide();
        }
        AppManager.Instance.ShowInTaskbar = visible;
    }

    protected override void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var saved = ConfigHandler.GetWindowSizeItem(_config, "DarfantCompact");
        Width = Math.Clamp(saved?.Width ?? 460, MinWidth, Math.Max(MinWidth, SystemParameters.WorkArea.Width));
        Height = Math.Clamp(saved?.Height ?? 740, MinHeight, Math.Max(MinHeight, SystemParameters.WorkArea.Height));
        if (_config.UiItem.AutoHideStartup)
        {
            ShowHideWindow(false);
        }
    }

    private void StorageUI()
    {
        if (WindowState == WindowState.Normal)
        {
            ConfigHandler.SaveWindowSizeItem(_config, "DarfantCompact", Width, Height);
        }
    }

    private void MoreOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e) => ShowHideWindow(true);
    private void ShowLog_Click(object sender, RoutedEventArgs e) => LogPanel.Visibility = Visibility.Visible;
    private void CloseLog_Click(object sender, RoutedEventArgs e) => LogPanel.Visibility = Visibility.Collapsed;

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        _backupView ??= new BackupAndRestoreView { ViewModel = ViewModel?.BackupAndRestoreViewModel };
        await DialogHost.Show(_backupView, "RootDialog");
    }

    private async void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (_exiting)
        {
            return;
        }
        _exiting = true;
        StorageUI();
        ViewModel?.ProfilesViewModel.ServerSpeedtestStop();
        tbNotify.Dispose();
        await AppManager.Instance.AppExitAsync(true);
    }
}
