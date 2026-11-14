using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace v2rayN.Common;

internal sealed record VpnApplication(string Name, string Path) : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected)
            {
                return;
            }
            _isSelected = value;
            PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
        }
    }
}

internal sealed class ObservableApplicationList
{
    public ObservableCollection<VpnApplication> Items { get; } = [];
    public ICollectionView View { get; }
    private readonly Dictionary<string, VpnApplication> _byPath = new(StringComparer.OrdinalIgnoreCase);

    public ObservableApplicationList(IEnumerable<string> selectedPaths)
    {
        View = CollectionViewSource.GetDefaultView(Items);
        View.SortDescriptions.Add(new(nameof(VpnApplication.Name), ListSortDirection.Ascending));
        foreach (var path in selectedPaths)
        {
            Add(new(Path.GetFileNameWithoutExtension(path), path), select: true);
        }
    }

    public void Add(VpnApplication app, bool select = false)
    {
        if (_byPath.TryGetValue(app.Path, out var existing))
        {
            existing.IsSelected |= select;
            return;
        }

        app.IsSelected = select;
        _byPath.Add(app.Path, app);
        Items.Add(app);
    }

    public void SetFilter(string query) => View.Filter = entry => entry is VpnApplication app
        && (app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || app.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
}

internal static class WindowsApplicationCatalog
{
    public static List<VpnApplication> Discover()
    {
        var apps = new Dictionary<string, VpnApplication>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path, string? label = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (Path.IsPathFullyQualified(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                apps.TryAdd(path, new(label ?? Path.GetFileNameWithoutExtension(path), path));
            }
        }

        ReadStartMenu(Add);
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var key in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths" })
            {
                using var appPaths = hive.OpenSubKey(key);
                foreach (var name in appPaths?.GetSubKeyNames() ?? [])
                {
                    using var registration = appPaths?.OpenSubKey(name);
                    Add(registration?.GetValue(null) as string);
                }
            }
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    Add(process.MainModule?.FileName);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // A process can exit or deny access while the list is being read.
                }
            }
        }
        return [.. apps.Values];
    }

    private static void ReadStartMenu(Action<string?, string?> add)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null || Activator.CreateInstance(shellType) is not { } shell)
        {
            return;
        }

        try
        {
            foreach (var folder in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms })
            {
                var root = Environment.GetFolderPath(folder);
                if (!Directory.Exists(root))
                {
                    continue;
                }
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
                foreach (var shortcutPath in Directory.EnumerateFiles(root, "*.lnk", options))
                {
                    try
                    {
                        dynamic shortcut = ((dynamic)shell).CreateShortcut(shortcutPath);
                        try
                        {
                            add((string)shortcut.TargetPath, Path.GetFileNameWithoutExtension(shortcutPath));
                        }
                        finally
                        {
                            Marshal.FinalReleaseComObject((object)shortcut);
                        }
                    }
                    catch (COMException)
                    {
                        // Broken shortcuts have no executable to show.
                    }
                }
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
