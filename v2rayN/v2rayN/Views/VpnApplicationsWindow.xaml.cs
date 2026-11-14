using Microsoft.Win32;
using System.Windows.Controls;

namespace v2rayN.Views;

public partial class VpnApplicationsWindow : Window
{
    private readonly ObservableApplicationList _applications;

    public List<string> SelectedPaths => _applications.Items.Where(item => item.IsSelected).Select(item => item.Path).ToList();

    public VpnApplicationsWindow(IEnumerable<string> selectedPaths)
    {
        _applications = new ObservableApplicationList(selectedPaths);
        InitializeComponent();
        ApplicationList.ItemsSource = _applications.View;
        UpdateSaveButton();
        Loaded += LoadApplications;
    }

    private async void LoadApplications(object sender, RoutedEventArgs e)
    {
        Loaded -= LoadApplications;
        try
        {
            foreach (var app in await Task.Run(WindowsApplicationCatalog.Discover))
            {
                _applications.Add(app);
            }
            DirectoryStatus.Text = _applications.Items.Count == 0 ? "فایل اجرایی برنامه را انتخاب کنید." : "";
            DirectoryStatus.Visibility = _applications.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(VpnApplicationsWindow), ex);
            DirectoryStatus.Text = "خواندن فهرست ممکن نبود. می‌توانید فایل اجرایی را انتخاب کنید.";
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => _applications.SetFilter(SearchBox.Text.Trim());

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "انتخاب فایل اجرایی برنامه",
            Filter = "برنامه‌های ویندوز (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            _applications.Add(new VpnApplication(Path.GetFileNameWithoutExtension(path), path), select: true);
        }
        SearchBox.Clear();
        _applications.View.Refresh();
        UpdateSaveButton();
    }

    private void Application_Click(object sender, RoutedEventArgs e) => UpdateSaveButton();
    private void UpdateSaveButton() => SaveButton.IsEnabled = _applications.Items.Any(item => item.IsSelected);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SaveButton.IsEnabled)
        {
            DialogResult = true;
        }
    }
}
