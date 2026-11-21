using System.Net.Http;
using Microsoft.Win32;
using ServiceLib.Services;

namespace v2rayN.Views;

public partial class MainWindow
{
    private static readonly HttpClient UpdateClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 1024 * 1024
    };
    private readonly DarfantUpdateService _updates = new(UpdateClient);
    private DispatcherTimer? _updateTimer;
    private DarfantUpdateService.Release? _availableRelease;
    private CancellationTokenSource? _updateDownload;
    private bool _checkingForUpdate;

    private void StartUpdateChecks()
    {
        if (_updateTimer != null)
        {
            return;
        }
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdateAsync(false);
        _updateTimer.Start();
        Closed += (_, _) =>
        {
            _updateTimer.Stop();
            _updateDownload?.Cancel();
        };
        _ = CheckForUpdateAsync(false);
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        ShowHideWindow(true);
        if (_updateDownload != null)
        {
            return;
        }
        if (_availableRelease != null)
        {
            UpdatePanel.Visibility = Visibility.Visible;
            UpdateAction.Focus();
            return;
        }
        await CheckForUpdateAsync(true);
    }

    private async Task CheckForUpdateAsync(bool manual)
    {
        if (_checkingForUpdate || _updateDownload != null || _exiting)
        {
            return;
        }
        _checkingForUpdate = true;
        if (manual)
        {
            MainSnackbar.MessageQueue?.Enqueue("در حال بررسی نسخهٔ جدید…");
        }
        try
        {
            var current = typeof(App).Assembly.GetName().Version!;
            var release = await _updates.CheckAsync(current, CancellationToken.None);
            if (_exiting)
            {
                return;
            }
            bool newlyAvailable = release != null && release.Version != _availableRelease?.Version;
            _availableRelease = release;
            UpdatePanel.Visibility = release != null ? Visibility.Visible : Visibility.Collapsed;
            if (release != null)
            {
                UpdateStatus.Text = $"نسخهٔ {release.Version} آماده است";
                UpdateAction.Content = "دانلود و نصب";
                UpdateAction.ToolTip = IsInstallerManaged()
                    ? "پس از دانلود، دارفانت بسته و نسخهٔ جدید نصب می‌شود."
                    : "نسخهٔ نصبی در پوشهٔ جدا نصب می‌شود. تنظیمات نسخهٔ قابل حمل در پوشهٔ خودش می‌ماند.";
                if (newlyAvailable && (!IsVisible || WindowState == WindowState.Minimized))
                {
                    tbNotify.ShowNotification("به‌روزرسانی دارفانت", $"نسخهٔ {release.Version} آماده است. دارفانت را باز کنید.", H.NotifyIcon.Core.NotificationIcon.Info);
                }
            }
            else if (manual)
            {
                MainSnackbar.MessageQueue?.Enqueue("دارفانت به‌روز است.");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Darfant update check failed", ex);
            if (manual && !_exiting)
            {
                MainSnackbar.MessageQueue?.Enqueue("بررسی نسخهٔ جدید ممکن نشد. دوباره تلاش کنید.");
            }
        }
        finally
        {
            _checkingForUpdate = false;
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateDownload != null)
        {
            _updateDownload.Cancel();
            return;
        }
        if (_availableRelease is not { } release || _checkingForUpdate || _exiting)
        {
            return;
        }
        var installed = IsInstallerManaged();
        if (!installed && MessageBox.Show(this,
                "نسخهٔ جدید در پوشهٔ نصب دارفانت قرار می‌گیرد. تنظیمات نسخهٔ قابل حمل در پوشهٔ فعلی باقی می‌ماند. نصب شروع شود؟",
                "نصب دارفانت", MessageBoxButton.OKCancel, MessageBoxImage.Information,
                MessageBoxResult.Cancel, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign) != MessageBoxResult.OK)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        _updateDownload = cancellation;
        string? path = null;
        bool installerStarted = false;
        UpdateAction.Content = "لغو دانلود";
        UpdateStatus.Text = "در حال دریافت نسخهٔ جدید…";
        try
        {
            path = Utils.GetTempPath($"Darfant-Setup-{Guid.NewGuid():N}.exe");
            var progress = new Progress<int>(percent =>
            {
                if (_updateDownload == cancellation)
                {
                    UpdateStatus.Text = percent < 100 ? $"در حال دانلود · {percent}٪" : "در حال بررسی فایل نصب…";
                }
            });
            await _updates.DownloadAsync(release, path, progress, cancellation.Token);
            await using (var verifiedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await DarfantUpdateService.VerifyAsync(release, verifiedFile, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (_exiting)
                {
                    return;
                }
                var start = new ProcessStartInfo(path) { UseShellExecute = false };
                start.ArgumentList.Add("/SP-");
                start.ArgumentList.Add("/SILENT");
                start.ArgumentList.Add("/NORESTART");
                start.ArgumentList.Add($"/DarfantUpdatePid={Environment.ProcessId}");
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start Darfant installer.");
                installerStarted = true;
            }
            UpdateStatus.Text = "در حال نصب نسخهٔ جدید…";
            UpdateAction.IsEnabled = false;
            await ExitDarfantAsync();
        }
        catch (OperationCanceledException)
        {
            if (!_exiting)
            {
                MainSnackbar.MessageQueue?.Enqueue("دانلود به‌روزرسانی متوقف شد.");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Darfant update installation failed", ex);
            if (!_exiting)
            {
                MainSnackbar.MessageQueue?.Enqueue("دریافت یا اجرای فایل نصب انجام نشد. دوباره تلاش کنید.");
            }
        }
        finally
        {
            _updateDownload = null;
            if (!installerStarted)
            {
                if (path != null)
                {
                    try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                UpdateStatus.Text = $"نسخهٔ {release.Version} آماده است";
                UpdateAction.Content = "دانلود و نصب";
            }
        }
    }

    private static bool IsInstallerManaged()
    {
        const string registration = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{7E56FFAE-074B-4ED6-AFF9-7E1E35DBE5A7}_is1";
        var location = Registry.CurrentUser.OpenSubKey(registration);
        using (location)
        {
            return location?.GetValue("InstallLocation") is string installedPath &&
                string.Equals(Path.TrimEndingDirectorySeparator(installedPath),
                    Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase);
        }
    }
}
