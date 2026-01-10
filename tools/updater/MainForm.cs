using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MuffModeUpdater;

internal sealed class MainForm : Form
{
    private const string RepoOwner = "themuffinator";
    private const string RepoName = "MuffMode-Map-Remasters";
    private const string RegistryPath = @"Software\MuffMode\MapRemasters\Updater";
    private const string RegistryInstallPath = "InstallPath";
    private const string RegistryAutoLaunch = "AutoLaunch";
    private const string RegistryLastVersion = "LastVersion";
    private const string VersionFileName = "muffmode-remasters.version";
    private const string ManifestFileName = "muffmode-remasters.manifest.json";

    private readonly TextBox installPathBox;
    private readonly Button browseButton;
    private readonly Button updateButton;
    private readonly Button launchButton;
    private readonly CheckBox autoLaunchCheckBox;
    private readonly TextBox logBox;
    private readonly Label statusLabel;

    private bool updateInProgress;

    public MainForm()
    {
        Text = "MuffMode Map Remasters Updater";
        MinimumSize = new System.Drawing.Size(820, 520);
        Width = 900;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(10),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var installLabel = new Label
        {
            Text = "Install path",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        };

        installPathBox = new TextBox
        {
            Dock = DockStyle.Fill,
        };

        browseButton = new Button
        {
            Text = "Browse...",
            Dock = DockStyle.Fill,
        };

        autoLaunchCheckBox = new CheckBox
        {
            Text = "Auto-launch after update",
            Dock = DockStyle.Fill,
        };

        updateButton = new Button
        {
            Text = "Check and Update",
            Width = 160,
            Height = 28,
        };

        launchButton = new Button
        {
            Text = "Launch Quake II",
            Width = 140,
            Height = 28,
            Enabled = false,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        buttonPanel.Controls.Add(updateButton);
        buttonPanel.Controls.Add(launchButton);

        logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new System.Drawing.Font("Consolas", 9.0f),
        };

        statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Text = "Ready.",
        };

        layout.Controls.Add(installLabel, 0, 0);
        layout.Controls.Add(installPathBox, 1, 0);
        layout.Controls.Add(browseButton, 2, 0);
        layout.Controls.Add(autoLaunchCheckBox, 0, 1);
        layout.SetColumnSpan(autoLaunchCheckBox, 3);
        layout.Controls.Add(buttonPanel, 0, 2);
        layout.SetColumnSpan(buttonPanel, 3);
        layout.Controls.Add(logBox, 0, 3);
        layout.SetColumnSpan(logBox, 3);
        layout.Controls.Add(statusLabel, 0, 4);
        layout.SetColumnSpan(statusLabel, 3);

        Controls.Add(layout);

        browseButton.Click += OnBrowseClicked;
        updateButton.Click += async (_, _) => await RunUpdateAsync();
        launchButton.Click += (_, _) => LaunchGame();
        autoLaunchCheckBox.CheckedChanged += (_, _) => SaveSettings();
        installPathBox.Leave += (_, _) => SaveSettings();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LoadSettings();
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select your Quake II install folder",
            UseDescriptionForTitle = true,
        };

        if (Directory.Exists(installPathBox.Text))
        {
            dialog.SelectedPath = installPathBox.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            installPathBox.Text = dialog.SelectedPath;
            SaveSettings();
        }
    }

    private async Task RunUpdateAsync()
    {
        if (updateInProgress)
        {
            return;
        }

        updateInProgress = true;
        updateButton.Enabled = false;
        launchButton.Enabled = false;

        try
        {
            ClearStatus("Checking for updates...");

            var installRoot = ResolveInstallRoot();
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                Log("Install path not set and auto-detection failed.");
                SetStatus("Install path required.");
                return;
            }

            installPathBox.Text = installRoot;

            var basePath = ResolveBasePath(installRoot);
            var mapsRoot = Path.Combine(basePath, "maps");
            Directory.CreateDirectory(mapsRoot);

            Log("Install root: " + installRoot);
            Log("Maps path: " + mapsRoot);

            var installedVersion = ReadInstalledVersion(basePath);
            if (!string.IsNullOrWhiteSpace(installedVersion))
            {
                Log("Installed version: " + installedVersion);
            }

            var release = await GetLatestReleaseAsync();
            if (release == null)
            {
                SetStatus("Failed to query releases.");
                return;
            }

            Log("Latest release: " + release.TagName);
            if (!string.IsNullOrWhiteSpace(release.Name))
            {
                Log("Release name: " + release.Name);
            }
            Log("Published: " + release.PublishedAt.ToLocalTime().ToString("u"));

            if (!string.IsNullOrWhiteSpace(installedVersion) &&
                string.Equals(installedVersion, release.TagName, StringComparison.OrdinalIgnoreCase))
            {
                Log("Already up to date.");
                SetStatus("No update available.");
                launchButton.Enabled = true;
                return;
            }

            var finalAsset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith("-final.zip", StringComparison.OrdinalIgnoreCase));
            var devAsset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith("-dev.zip", StringComparison.OrdinalIgnoreCase));

            if (finalAsset == null)
            {
                Log("Missing final zip asset. Update aborted.");
                SetStatus("Update failed.");
                return;
            }

            Log("Final asset: " + finalAsset.Name);
            if (devAsset != null)
            {
                Log("Dev asset: " + devAsset.Name);
            }
            else
            {
                Log("Dev asset not found. Dev maps will be skipped.");
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "MuffModeMapRemasters", release.TagName);
            Directory.CreateDirectory(tempRoot);

            var finalZip = await DownloadAssetAsync(finalAsset, tempRoot);
            if (finalZip == null)
            {
                SetStatus("Update failed.");
                return;
            }

            string? devZip = null;
            if (devAsset != null)
            {
                devZip = await DownloadAssetAsync(devAsset, tempRoot);
                if (devZip == null)
                {
                    Log("Dev maps download failed. Continuing with final maps.");
                }
            }

            var manifest = LoadManifest(basePath);
            var finalUpdated = false;
            var devUpdated = false;

            var finalMaps = ExtractMapsFromZip(finalZip, mapsRoot, "final");
            if (finalMaps.Count > 0)
            {
                finalUpdated = true;
                RemoveStaleMaps(mapsRoot, manifest.FinalMaps, finalMaps, "final");
                manifest.FinalMaps = finalMaps;
            }

            if (!string.IsNullOrWhiteSpace(devZip))
            {
                var devMaps = ExtractMapsFromZip(devZip, mapsRoot, "dev");
                if (devMaps.Count > 0)
                {
                    devUpdated = true;
                    RemoveStaleMaps(mapsRoot, manifest.DevMaps, devMaps, "dev");
                    manifest.DevMaps = devMaps;
                }
            }

            if (finalUpdated || devUpdated)
            {
                manifest.Version = release.TagName;
                SaveManifest(basePath, manifest);
                WriteInstalledVersion(basePath, release.TagName);
                SaveRegistryValue(RegistryLastVersion, release.TagName, RegistryValueKind.String);
            }

            Log("Update complete.");
            SetStatus("Update complete.");
            launchButton.Enabled = true;

            if (autoLaunchCheckBox.Checked)
            {
                LaunchGame();
            }
        }
        catch (Exception ex)
        {
            Log("Update failed: " + ex.Message);
            SetStatus("Update failed.");
        }
        finally
        {
            updateButton.Enabled = true;
            updateInProgress = false;
        }
    }

    private string? ResolveInstallRoot()
    {
        var current = installPathBox.Text.Trim();
        if (IsQuake2Root(current))
        {
            return current;
        }

        var fromRegistry = ReadRegistryValue(RegistryInstallPath) as string;
        if (!string.IsNullOrWhiteSpace(fromRegistry) && IsQuake2Root(fromRegistry))
        {
            Log("Using install path from registry.");
            return fromRegistry;
        }

        var detected = FindInstallPath();
        if (!string.IsNullOrWhiteSpace(detected))
        {
            Log("Detected install path: " + detected);
            SaveRegistryValue(RegistryInstallPath, detected, RegistryValueKind.String);
            return detected;
        }

        return null;
    }

    private string ResolveBasePath(string installRoot)
    {
        var rereleaseBase = Path.Combine(installRoot, "rerelease", "baseq2");
        if (Directory.Exists(rereleaseBase))
        {
            return rereleaseBase;
        }

        return Path.Combine(installRoot, "baseq2");
    }

    private void LaunchGame()
    {
        var installRoot = installPathBox.Text.Trim();
        if (!IsQuake2Root(installRoot))
        {
            Log("Install path is invalid. Cannot launch.");
            return;
        }

        var exePath = FindGameExecutable(installRoot);
        if (string.IsNullOrWhiteSpace(exePath))
        {
            Log("Quake II executable not found.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? installRoot,
            });
        }
        catch (Exception ex)
        {
            Log("Launch failed: " + ex.Message);
        }
    }

    private static string? FindGameExecutable(string installRoot)
    {
        var candidates = new[]
        {
            Path.Combine(installRoot, "Quake2.exe"),
            Path.Combine(installRoot, "quake2.exe"),
            Path.Combine(installRoot, "rerelease", "Quake2.exe"),
            Path.Combine(installRoot, "rerelease", "quake2.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private ReleaseInfo? GetLatestReleaseFallback(string json)
    {
        try
        {
            return ParseRelease(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ReleaseInfo?> GetLatestReleaseAsync()
    {
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuffModeUpdater", "1.0"));

        try
        {
            var response = await client.GetAsync(url);
            var payload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Log("Release query failed: " + response.StatusCode);
                var fallback = GetLatestReleaseFallback(payload);
                if (fallback != null)
                {
                    Log("Using fallback parser.");
                    return fallback;
                }
                return null;
            }

            return ParseRelease(payload);
        }
        catch (Exception ex)
        {
            Log("Release query failed: " + ex.Message);
            return null;
        }
    }

    private static ReleaseInfo ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
        var publishedAt = root.TryGetProperty("published_at", out var publishedProp)
            ? publishedProp.GetDateTimeOffset()
            : DateTimeOffset.MinValue;

        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var assetsProp))
        {
            foreach (var asset in assetsProp.EnumerateArray())
            {
                var assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                var url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                var size = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                if (!string.IsNullOrWhiteSpace(assetName) && !string.IsNullOrWhiteSpace(url))
                {
                    assets.Add(new ReleaseAsset(assetName, url, size));
                }
            }
        }

        return new ReleaseInfo(tagName, name, publishedAt, assets);
    }

    private async Task<string?> DownloadAssetAsync(ReleaseAsset asset, string targetDir)
    {
        try
        {
            var targetPath = Path.Combine(targetDir, asset.Name);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            Log("Downloading " + asset.Name + "...");
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuffModeUpdater", "1.0"));

            using var response = await client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(output);

            Log("Downloaded " + asset.Name);
            return targetPath;
        }
        catch (Exception ex)
        {
            Log("Download failed for " + asset.Name + ": " + ex.Message);
            return null;
        }
    }

    private List<string> ExtractMapsFromZip(string zipPath, string mapsRoot, string label)
    {
        var extracted = new List<string>();
        Log("Extracting " + label + " maps...");

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetMapsRelativePath(entry.FullName, out var relativePath))
            {
                relativePath = Path.GetFileName(entry.FullName);
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(mapsRoot, relativePath));
            var mapsRootFull = Path.GetFullPath(mapsRoot) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(mapsRootFull, StringComparison.OrdinalIgnoreCase))
            {
                Log("Skipped unexpected entry: " + entry.FullName);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? mapsRoot);
            entry.ExtractToFile(destination, true);
            extracted.Add(relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
        }

        Log("Extracted " + extracted.Count + " " + label + " maps.");
        return extracted.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool TryGetMapsRelativePath(string entryName, out string relativePath)
    {
        var normalized = entryName.Replace('\\', '/');
        var prefixes = new[] { "baseq2/maps/", "maps/" };

        foreach (var prefix in prefixes)
        {
            var idx = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                relativePath = normalized[(idx + prefix.Length)..];
                return !string.IsNullOrWhiteSpace(relativePath);
            }
        }

        relativePath = string.Empty;
        return false;
    }

    private void RemoveStaleMaps(string mapsRoot, IReadOnlyCollection<string> oldMaps, IReadOnlyCollection<string> newMaps, string label)
    {
        if (oldMaps.Count == 0)
        {
            return;
        }

        var newSet = new HashSet<string>(newMaps, StringComparer.OrdinalIgnoreCase);
        foreach (var map in oldMaps)
        {
            if (newSet.Contains(map))
            {
                continue;
            }

            var targetPath = Path.Combine(mapsRoot, map);
            if (File.Exists(targetPath))
            {
                try
                {
                    File.Delete(targetPath);
                    Log("Removed stale " + label + " map: " + map);
                }
                catch (Exception ex)
                {
                    Log("Failed to remove " + map + ": " + ex.Message);
                }
            }
        }
    }

    private void LoadSettings()
    {
        var installPath = ReadRegistryValue(RegistryInstallPath) as string;
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            installPathBox.Text = installPath;
        }
        else
        {
            var detected = FindInstallPath();
            if (!string.IsNullOrWhiteSpace(detected))
            {
                installPathBox.Text = detected;
            }
        }

        var autoLaunch = ReadRegistryValue(RegistryAutoLaunch);
        autoLaunchCheckBox.Checked = autoLaunch is int value && value != 0;
    }

    private void SaveSettings()
    {
        var path = installPathBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SaveRegistryValue(RegistryInstallPath, path, RegistryValueKind.String);
        }

        SaveRegistryValue(RegistryAutoLaunch, autoLaunchCheckBox.Checked ? 1 : 0, RegistryValueKind.DWord);
    }

    private static object? ReadRegistryValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue(name);
    }

    private static void SaveRegistryValue(string name, object value, RegistryValueKind kind)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key?.SetValue(name, value, kind);
    }

    private static string? ReadInstalledVersion(string basePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        var value = key?.GetValue(RegistryLastVersion) as string;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var versionFile = Path.Combine(basePath, VersionFileName);
        if (File.Exists(versionFile))
        {
            return File.ReadAllText(versionFile).Trim();
        }

        return null;
    }

    private static void WriteInstalledVersion(string basePath, string version)
    {
        var versionFile = Path.Combine(basePath, VersionFileName);
        File.WriteAllText(versionFile, version, Encoding.ASCII);
    }

    private static MapsManifest LoadManifest(string basePath)
    {
        var path = Path.Combine(basePath, ManifestFileName);
        if (!File.Exists(path))
        {
            return new MapsManifest();
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<MapsManifest>(json);
            return manifest ?? new MapsManifest();
        }
        catch
        {
            return new MapsManifest();
        }
    }

    private static void SaveManifest(string basePath, MapsManifest manifest)
    {
        var path = Path.Combine(basePath, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private string? FindInstallPath()
    {
        var steam = FindSteamInstall();
        if (!string.IsNullOrWhiteSpace(steam))
        {
            return steam;
        }

        var gog = FindGogInstall();
        if (!string.IsNullOrWhiteSpace(gog))
        {
            return gog;
        }

        var epic = FindEpicInstall();
        if (!string.IsNullOrWhiteSpace(epic))
        {
            return epic;
        }

        var fallback = "C:\\Games\\Quake 2";
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static bool IsQuake2Root(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Directory.Exists(path) &&
               (File.Exists(Path.Combine(path, "quake2.exe")) ||
                File.Exists(Path.Combine(path, "Quake2.exe")) ||
                Directory.Exists(Path.Combine(path, "baseq2")) ||
                Directory.Exists(Path.Combine(path, "rerelease")));
    }

    private static string? FindSteamInstall()
    {
        var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string ??
                        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            return null;
        }

        steamPath = NormalizePath(steamPath);
        var candidate = Path.Combine(steamPath, "steamapps", "common", "Quake 2");
        if (IsQuake2Root(candidate))
        {
            return candidate;
        }

        candidate = Path.Combine(steamPath, "steamapps", "common", "Quake II");
        if (IsQuake2Root(candidate))
        {
            return candidate;
        }

        var libraries = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraries))
        {
            return null;
        }

        foreach (var lib in ParseSteamLibraries(libraries))
        {
            candidate = Path.Combine(lib, "steamapps", "common", "Quake 2");
            if (IsQuake2Root(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(lib, "steamapps", "common", "Quake II");
            if (IsQuake2Root(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> ParseSteamLibraries(string vdfPath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(vdfPath);
        }
        catch
        {
            yield break;
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var path = ExtractSecondQuoted(trimmed);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!LooksLikePath(path))
            {
                continue;
            }

            yield return NormalizePath(path);
        }
    }

    private static string? FindGogInstall()
    {
        var gog = FindGogInstallFromRegistry(RegistryView.Registry64, @"SOFTWARE\GOG.com\Games");
        if (!string.IsNullOrWhiteSpace(gog))
        {
            return gog;
        }

        gog = FindGogInstallFromRegistry(RegistryView.Registry32, @"SOFTWARE\GOG.com\Games");
        if (!string.IsNullOrWhiteSpace(gog))
        {
            return gog;
        }

        var defaultPaths = new[]
        {
            "C:\\GOG Games\\Quake II",
            "C:\\Program Files (x86)\\GOG Galaxy\\Games\\Quake II",
        };

        return defaultPaths.FirstOrDefault(IsQuake2Root);
    }

    private static string? FindGogInstallFromRegistry(RegistryView view, string rootPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var root = baseKey.OpenSubKey(rootPath);
            if (root == null)
            {
                return null;
            }

            foreach (var keyName in root.GetSubKeyNames())
            {
                using var gameKey = root.OpenSubKey(keyName);
                if (gameKey == null)
                {
                    continue;
                }

                var pathValue = gameKey.GetValue("path") as string;
                if (string.IsNullOrWhiteSpace(pathValue))
                {
                    continue;
                }

                var nameValue = gameKey.GetValue("gameName") as string ??
                                gameKey.GetValue("GameName") as string ??
                                gameKey.GetValue("DisplayName") as string ??
                                gameKey.GetValue("name") as string ?? string.Empty;

                if (nameValue.Length == 0 ||
                    nameValue.Contains("Quake II", StringComparison.OrdinalIgnoreCase) ||
                    nameValue.Contains("Quake 2", StringComparison.OrdinalIgnoreCase))
                {
                    var normalized = NormalizePath(pathValue);
                    if (IsQuake2Root(normalized))
                    {
                        return normalized;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? FindEpicInstall()
    {
        var manifestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifestDir))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(manifestDir, "*.item"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;
                var displayName = GetJsonString(root, "DisplayName");
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                if (!displayName.Contains("Quake II", StringComparison.OrdinalIgnoreCase) &&
                    !displayName.Contains("Quake 2", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var installLocation = GetJsonString(root, "InstallLocation");
                if (string.IsNullOrWhiteSpace(installLocation))
                {
                    continue;
                }

                var normalized = NormalizePath(UnescapeJsonPath(installLocation));
                if (IsQuake2Root(normalized))
                {
                    return normalized;
                }
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    private static string GetJsonString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string NormalizePath(string path)
    {
        path = path.Replace('/', '\\');
        if (path.EndsWith("\\", StringComparison.Ordinal))
        {
            path = path[..^1];
        }

        return path;
    }

    private static string UnescapeJsonPath(string value)
    {
        return value.Replace("\\\\", "\\");
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains(":\\", StringComparison.Ordinal) || value.Contains(":/", StringComparison.Ordinal);
    }

    private static string ExtractSecondQuoted(string line)
    {
        var firstQuote = line.IndexOf('"');
        if (firstQuote < 0)
        {
            return string.Empty;
        }

        var secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0)
        {
            return string.Empty;
        }

        var thirdQuote = line.IndexOf('"', secondQuote + 1);
        if (thirdQuote < 0)
        {
            return string.Empty;
        }

        var fourthQuote = line.IndexOf('"', thirdQuote + 1);
        if (fourthQuote < 0)
        {
            return string.Empty;
        }

        return line.Substring(thirdQuote + 1, fourthQuote - thirdQuote - 1);
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        logBox.AppendText("[" + timestamp + "] " + message + Environment.NewLine);
    }

    private void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus), status);
            return;
        }

        statusLabel.Text = status;
    }

    private void ClearStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(ClearStatus), status);
            return;
        }

        statusLabel.Text = status;
        logBox.Clear();
    }

    private sealed record ReleaseInfo(string TagName, string Name, DateTimeOffset PublishedAt, List<ReleaseAsset> Assets);

    private sealed record ReleaseAsset(string Name, string DownloadUrl, long Size);

    private sealed class MapsManifest
    {
        public string Version { get; set; } = string.Empty;
        public List<string> FinalMaps { get; set; } = new();
        public List<string> DevMaps { get; set; } = new();
    }
}
