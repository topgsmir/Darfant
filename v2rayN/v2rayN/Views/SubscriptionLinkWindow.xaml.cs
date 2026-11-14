using System.Windows.Controls;

namespace v2rayN.Views;

public partial class SubscriptionLinkWindow : Window
{
    public Uri? Link { get; private set; }

    public SubscriptionLinkWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LinkInput.Focus();
    }

    private void LinkInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var input = LinkInput.Text.Trim();
        Link = Uri.TryCreate(input, UriKind.Absolute, out var link) && link.Scheme is "https" or "http"
            ? link : null;
        LinkError.Visibility = Link == null && input.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.IsEnabled = Link != null;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (Link != null)
        {
            DialogResult = true;
        }
    }
}
