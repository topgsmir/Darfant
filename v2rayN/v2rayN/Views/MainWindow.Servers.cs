using System.Windows.Controls;

namespace v2rayN.Views;

public partial class MainWindow
{
    private ICollectionView? _serverView;
    private EServerOperator _selectedOperator;

    private void InitializeServerList(MainWindowViewModel model)
    {
        _serverView = CollectionViewSource.GetDefaultView(model.ProfilesViewModel.ProfileItems);
        _serverView.Filter = value => value is ProfileItemModel server
            && ServerLabels.MatchesOperator(_selectedOperator, ServerLabels.GetOperator(server, _config.ServerOperatorOverrides));
        ServerList.ItemsSource = _serverView;
    }

    private void OperatorCategory_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string value })
        {
            _selectedOperator = (EServerOperator)int.Parse(value, CultureInfo.InvariantCulture);
            _serverView?.Refresh();
        }
    }

    private async void AssignOperator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ProfileItemModel server, Tag: string value })
        {
            return;
        }
        var key = ServerLabels.ProfileKey(server);
        if (value == "auto")
        {
            _config.ServerOperatorOverrides.Remove(key);
        }
        else
        {
            _config.ServerOperatorOverrides[key] = (EServerOperator)int.Parse(value, CultureInfo.InvariantCulture);
        }
        _serverView?.Refresh();
        if (await ConfigHandler.SaveConfig(_config) != 0)
        {
            NoticeManager.Instance.Enqueue("ذخیرهٔ دسته‌بندی انجام نشد.");
        }
    }
}
