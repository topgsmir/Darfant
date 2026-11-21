# Darfant customization map

Use `maintenance/dependencies.json` → `v2rayN.commit` as the comparison base. Preserve upstream history with merge commits. New upstream behavior still needs review when a clean textual merge changes shared assumptions.

| Area | Darfant behavior and integration points |
| --- | --- |
| Branding/build | `Darfant/Branding.props`, `Darfant/app.manifest`, `Darfant/Windows.targets`. The WPF `.csproj` has a single fork import; the original manifest matches the upstream baseline. `ServiceLib/Global.cs` contains the display name and subscription interval; `Common/Utils.cs` has the Darfant user-data directory. |
| Compact Windows UI | `Views/MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.Darfant.cs`, `MainWindow.Servers.cs`, `Resources/DarfantTheme.xaml`, `WindowsManager.cs`. Preserve the compact responsive layout and event bindings when adapting upstream view models. |
| Subscription import/update | `SubscriptionLinkWindow`, `SubscriptionHandler`, `ConfigHandler`, `TaskManager`, `SubItem`, `SubEditViewModel`, `MainWindowViewModel`. Immediate import, enabled subscriptions refreshed every 30 minutes, old servers retained if import fails. |
| Operators and flags | `ServerLabels`, `EServerOperator`, `Config.ServerOperatorOverrides`, `ProfilesViewModel`, `MainWindow.Servers.cs`, `ServerFlagConverter`, `Resources/Flags`. Infer from server/subscription names, preserve manual overrides after reimport, render PNG flags. Run `ServerLabelsTests`, including `همراه_اول` and `همه_اپراتورها`. |
| System/browser/application scope | `ETunScope`, `ConfigItems`, `WindowsVpnScope`, `WindowsApplicationCatalog`, `VpnApplicationsWindow`, `StatusBarViewModel`, `SingboxConfig`. Process matching uses sing-box TUN; legacy protection adapts scoped modes. |
| Iran bypass | `CoreConfigClashService`, `SingboxRoutingService`, `SingboxDnsService`, `V2rayRoutingService`, `V2rayDnsService`, `ConfigHandler`, `windows-assets/geoip-ir.srs`. `.ir` and Iran IP bypass are applied to generated configurations. Complete custom JSON retains its own routing. Preserve rule ordering and DNS routing while adopting upstream config/schema changes. |
| Ping completion | `SpeedtestService`, `ConnectionHandler`, `ProfileExManager`, `SqliteHelper`. Finalize skipped/failed nodes, enforce metadata deadlines, persist results safely, retry only the failed subset. Run `SpeedtestServiceBatchTests`. |
| Updater/data safety | GUI self-update via upstream is disabled in `CoreInfoManager` to preserve the Darfant distribution; official core update support remains. `AppManager` preserves Persian UI culture. |

Upstream `.github/workflows` and Dependabot configuration are intentionally excluded by the update helper; Darfant's workflows and README are restored from the starting commit. The new upstream README is stored in `README.upstream.md`. Other code conflicts remain explicit and must be resolved rather than globally taking our version.

The baseline tree omits the unused `GlobalHotKeys` submodule gitlink. If upstream changes its integration, review any modify/delete conflict together with the Windows project references before deciding whether to restore the submodule.

Xray stays an external executable. Its official binary, version, archive digest and executable digest are recorded independently; core upgrades do not replace the WPF UI or Darfant settings. The bundle supplies the other engines and supporting assets until they are separately upgraded.
