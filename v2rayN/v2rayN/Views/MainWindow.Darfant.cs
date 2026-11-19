using System.Windows.Controls;
using ServiceLib.Models.Dto;

namespace v2rayN.Views;

public partial class MainWindow
{
    private bool _updatingVpnScope;
    private bool _pingRunning;

    private async void ImportLink_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        var dialog = new SubscriptionLinkWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Link is not { } link)
        {
            return;
        }

        btnImportLink.IsEnabled = false;
        btnImportLink.Content = "دریافت اشتراک…";
        try
        {
            await model.ImportSubscriptionAsync(link);
        }
        finally
        {
            btnImportLink.IsEnabled = true;
            btnImportLink.Content = "وارد کردن لینک";
        }
    }

    private async void RefreshLinks_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        btnRefreshLinks.IsEnabled = false;
        try
        {
            await model.UpdateSubscriptionProcess("", true);
        }
        finally
        {
            btnRefreshLinks.IsEnabled = true;
        }
    }

    private async void PingAll_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } model)
        {
            if (_pingRunning)
            {
                model.ProfilesViewModel.ServerSpeedtestStop();
                return;
            }
            _pingRunning = true;
            PingLabel.Text = "توقف تست";
            try
            {
                await model.ProfilesViewModel.ServerSpeedtest(ESpeedActionType.Realping, allSubscriptions: true);
            }
            finally
            {
                _pingRunning = false;
                PingLabel.Text = "تست پینگ همگی";
            }
        }
    }

    private void RefreshVpnScope()
    {
        _updatingVpnScope = true;
        scopeSystem.IsChecked = _config.TunModeItem.Scope == ETunScope.System;
        scopeBrowsers.IsChecked = _config.TunModeItem.Scope == ETunScope.Browsers;
        scopeApplications.IsChecked = _config.TunModeItem.Scope == ETunScope.Applications;
        btnChooseApplications.Visibility = _config.TunModeItem.Scope == ETunScope.Applications
            ? Visibility.Visible : Visibility.Collapsed;
        btnChooseApplications.Content = $"انتخاب برنامه‌ها · {_config.TunModeItem.ApplicationPaths?.Count ?? 0} برنامه";
        _updatingVpnScope = false;
    }

    private async void VpnScope_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingVpnScope || ViewModel is not { } model || sender is not RadioButton { Tag: string value })
        {
            return;
        }

        var scope = (ETunScope)int.Parse(value, CultureInfo.InvariantCulture);
        if (scope == _config.TunModeItem.Scope)
        {
            return;
        }
        if (scope == ETunScope.Applications)
        {
            await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            await ChooseApplicationsAsync(model);
            return;
        }

        _config.TunModeItem.Scope = scope;
        await SaveVpnScopeAsync(model);
    }

    private async void ChooseApplications_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } model)
        {
            await ChooseApplicationsAsync(model);
        }
    }

    private async Task ChooseApplicationsAsync(MainWindowViewModel model)
    {
        var picker = new VpnApplicationsWindow(_config.TunModeItem.ApplicationPaths ?? []) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            _config.TunModeItem.ApplicationPaths = picker.SelectedPaths;
            _config.TunModeItem.Scope = ETunScope.Applications;
            await SaveVpnScopeAsync(model);
        }
        RefreshVpnScope();
    }

    private async Task SaveVpnScopeAsync(MainWindowViewModel model)
    {
        RefreshVpnScope();
        await ConfigHandler.SaveConfig(_config);
        if (_config.TunModeItem.EnableTun)
        {
            await model.Reload();
        }
    }

    private async void ToggleDarfantVpn_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        var enabled = !model.StatusBarViewModel.EnableTun;
        togDarfantVpn.IsEnabled = false;
        try
        {
            if (enabled && await ConfigHandler.GetDefaultServer(_config) == null)
            {
                togDarfantVpn.IsChecked = false;
                NoticeManager.Instance.Enqueue("ابتدا لینک اشتراک را وارد کنید.");
                return;
            }

            if (enabled && model.ProfilesViewModel.SelectedProfile is { IndexId.Length: > 0 } selected)
            {
                await model.ProfilesViewModel.SetDefaultServer(selected.IndexId);
            }
            model.StatusBarViewModel.EnableTun = enabled;
        }
        finally
        {
            RefreshConnectionState();
        }
    }

    private async void Server_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } model && sender is Button { Tag: ProfileItemModel server })
        {
            model.ProfilesViewModel.SelectedProfile = server;
            await model.ProfilesViewModel.SetDefaultServer(server.IndexId);
            RefreshConnectionState();
        }
    }

    private async void ServerList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel is { } model)
        {
            await model.ProfilesViewModel.SetDefaultServer();
            RefreshConnectionState();
            e.Handled = true;
        }
    }
}
