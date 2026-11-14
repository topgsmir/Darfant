using System.Windows.Controls;

namespace v2rayN.Views;

public partial class MainWindow
{
    private bool _updatingVpnScope;

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
        try
        {
            await model.ImportSubscriptionAsync(link);
        }
        finally
        {
            btnImportLink.IsEnabled = true;
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
            btnPingAll.IsEnabled = false;
            try
            {
                await model.ProfilesViewModel.ServerSpeedtest(ESpeedActionType.Realping, allSubscriptions: true);
            }
            finally
            {
                btnPingAll.IsEnabled = true;
            }
        }
    }

    private void RefreshVpnScope()
    {
        _updatingVpnScope = true;
        cmbVpnScope.SelectedIndex = (int)_config.TunModeItem.Scope;
        btnChooseApplications.Visibility = _config.TunModeItem.Scope == ETunScope.Applications
            ? Visibility.Visible : Visibility.Collapsed;
        _updatingVpnScope = false;
    }

    private async void VpnScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingVpnScope || _config == null || ViewModel is not { } model || cmbVpnScope.SelectedIndex < 0)
        {
            return;
        }

        var scope = (ETunScope)cmbVpnScope.SelectedIndex;
        if (scope == ETunScope.Applications)
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
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

        var enabled = togDarfantVpn.IsChecked == true;
        togDarfantVpn.IsEnabled = false;
        try
        {
            if (enabled && await ConfigHandler.GetDefaultServer(_config) == null)
            {
                togDarfantVpn.IsChecked = false;
                NoticeManager.Instance.Enqueue("ابتدا لینک اشتراک را وارد کنید.");
                return;
            }

            model.StatusBarViewModel.EnableTun = enabled;
        }
        finally
        {
            togDarfantVpn.IsEnabled = true;
        }
    }
}
