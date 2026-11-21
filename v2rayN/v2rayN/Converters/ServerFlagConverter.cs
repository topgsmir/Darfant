using System.Windows.Media.Imaging;

namespace v2rayN.Converters;

public sealed class ServerFlagConverter : IMultiValueConverter
{
    private static readonly Dictionary<string, BitmapImage> Images = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var code = ServerLabels.CountryCode(values.ElementAtOrDefault(0) as string, values.ElementAtOrDefault(1) as string) ?? "unknown";
        if (Images.TryGetValue(code, out var cached))
        {
            return cached;
        }
        try
        {
            return Load(code);
        }
        catch (IOException)
        {
            return Load("unknown");
        }
    }

    private static BitmapImage Load(string code)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/Darfant;component/Resources/Flags/{code.ToLowerInvariant()}.png");
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        Images[code] = image;
        return image;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class PingResultConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "—";
        }
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delay))
        {
            return delay > 0 ? $"{delay} ms" : "ناموفق";
        }
        return text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
