using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Threading;
using AvaloniaXplat.Extensions;
using AvaloniaXplat.Helpers;
using AvaloniaXplat.Models;
using AvaloniaXplat.SdbClient;
using AvaloniaXplat.Services;
using AvaloniaXplat.SigninManager;
using AvaloniaXplat.Views;

namespace AvaloniaXplat.Services
{
    public class TizenInstallerService : ITizenInstallerService
    {
        private static readonly string[] PossibleTizenPaths = GetPossibleTizenPaths();

        private static string[] GetPossibleTizenPaths()
        {
            var paths = new List<string>();

            if (OperatingSystem.IsWindows())
            {
                paths.Add(@"C:\TizenStudioCli");
                paths.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "TizenStudioCli"));
            }
            else if (OperatingSystem.IsMacOS())
            {
                paths.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "TizenStudioCli"));
            }
            else if (OperatingSystem.IsLinux())
            {
                paths.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".tizen-studio-cli"));
            }

            return paths.ToArray();
        }

        private readonly HttpClient _httpClient;
        private readonly IDialogService _dialogService;
        private readonly AppSettings _appSettings;
        private readonly JellyfinHelper _jellyfinHelper;
        private readonly OperatingSystemHelper _osHelper;
        private readonly ProcessHelper _processHelper;
        private readonly FileHelper _fileHelper;
        private readonly IBrowserService _browserService;
        private readonly string _downloadDirectory;
        private string _installPath;
        private const int MaxSafePathLength = 240;

        public string? TizenRootPath { get; private set; }
        public string? TizenCliPath { get; private set; }
        public string? TizenSdbPath { get; private set; }
        public string? TizenCypto { get; private set; }
        public string? TizenPluginPath { get; private set; }
        public string? TizenDataPath { get; private set; }
        public string? PackageCertificate { get; set; }

        public TizenInstallerService(
            HttpClient httpClient,
            IDialogService dialogService,
            AppSettings appSettings,
            JellyfinHelper jellyfinHelper,
            OperatingSystemHelper osHelper,
            ProcessHelper processHelper,
            FileHelper fileHelper,
            IBrowserService browserService)
        {
            _httpClient = httpClient;
            _dialogService = dialogService;
            _appSettings = appSettings;
            _jellyfinHelper = jellyfinHelper;
            _osHelper = osHelper;
            _processHelper = processHelper;
            _fileHelper = fileHelper;
            _browserService = browserService;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SamsungJellyfinInstaller/1.0");

            _downloadDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SamsungJellyfinInstaller",
                "Downloads");

            Directory.CreateDirectory(_downloadDirectory);

            DetermineInstallPath();
            InitializeTizenPaths();
        }

        private void InitializeTizenPaths()
        {
            string? tizenRoot = FindTizenRoot();

            if (tizenRoot is not null)
            {
                TizenRootPath = tizenRoot;

                // CLI launcher
                TizenCliPath = Path.Combine(
                    tizenRoot, "tools", "ide", "bin",
                    OperatingSystem.IsWindows() ? "tizen.bat" : "tizen"
                );

                // SDB
                TizenSdbPath = Path.Combine(
                    tizenRoot, "tools",
                    OperatingSystem.IsWindows() ? "sdb.exe" : "sdb"
                );

                // Crypto tool (Windows only)
                TizenCypto = OperatingSystem.IsWindows()
                    ? Path.Combine(tizenRoot, "tools", "certificate-encryptor", "wincrypt.exe")
                    : null; // no wincrypt on Linux/macOS

                // Plugins
                TizenPluginPath = Path.Combine(tizenRoot, "ide", "plugins");

                // Data path (profiles.xml)
                string tizenDataRoot = Path.Combine(
                    Path.GetDirectoryName(tizenRoot) ?? tizenRoot,
                    Path.GetFileName(tizenRoot) + "-data"
                );
                TizenDataPath = Path.Combine(tizenDataRoot, "profile", "profiles.xml");
            }
            else
            {
                TizenRootPath = null;
                TizenCliPath = null;
                TizenSdbPath = null;
                TizenCypto = null;
                TizenPluginPath = null;
                TizenDataPath = null;
            }
        }

        private void DetermineInstallPath()
        {
            string defaultPath;

            if (OperatingSystem.IsWindows())
            {
                defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "TizenStudioCli"
                );
            }
            else if (OperatingSystem.IsMacOS())
            {
                defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "TizenStudioCli"
                );
            }
            else // Linux
            {
                defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".tizen-studio-cli"
                );
            }

            // Fallback only for Windows (path length issues)
            var fallbackPath = OperatingSystem.IsWindows()
                ? "C:\\TizenStudioCli"
                : defaultPath;

            if (defaultPath.Length > MaxSafePathLength)
            {
                _dialogService.ShowMessageAsync("Path length exceeded",
                    "Path length exceeded the safe limit. Using fallback path.").Wait();
                _installPath = fallbackPath;
            }
            else
            {
                _installPath = defaultPath;
            }
        }

        private static bool FolderHasCertJar(string path)
        {
            if (!Directory.Exists(path))
                return false;

            return Directory.EnumerateFiles(path, "*.jar")
                .Any(file => Path.GetFileName(file)
                    .StartsWith("org.tizen.common.cert_", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> ConnectToTvAsync(string tvIpAddress)
        {
            if (TizenSdbPath is null)
                return false;

            try
            {
                var result = await _processHelper.RunCommandAsync(TizenSdbPath, $"connect {tvIpAddress}");
                return result.Output.Contains($"connected to {tvIpAddress}");
            }
            catch
            {
                return false;
            }
        }

        public async Task<Stream> DownloadPackageAsync(string downloadUrl)
        {
            Directory.CreateDirectory(_downloadDirectory);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            var fileStream = new MemoryStream();

            await contentStream.CopyToAsync(fileStream);

            return fileStream;
        }

        public async Task<InstallResult> InstallPackageAsync(Stream packageStream, string tvIpAddress,
            ProgressCallback? progress = null)
        {
            await using var sdbClient = new SdbTcpDevice(IPAddress.Parse(tvIpAddress));

            try
            {
                progress?.Invoke("ConnectingToDevice".Localized());
                await sdbClient.ConnectAsync();

                string tvName = GetTvNameAsync(sdbClient);
                if (string.IsNullOrEmpty(tvName))
                {
                    progress?.Invoke("TvNameNotFound".Localized());
                    return InstallResult.FailureResult("TvNameNotFound".Localized());
                }

                string tvDuid = await GetTvDuidAsync(sdbClient);
                if (string.IsNullOrEmpty(tvDuid))
                {
                    progress?.Invoke("TvDuidNotFound".Localized());
                    return InstallResult.FailureResult("TvDuidNotFound".Localized());
                }

                string tizenOs = await FetchTizenOsAsync(sdbClient);
                if (string.IsNullOrEmpty(tizenOs))
                    tizenOs = "7.0";


                bool needsCertificates = new Version(tizenOs) >= new Version("7.0") ||
                                         AppSettings.Default.ConfigUpdateMode != "None" ||
                                         AppSettings.Default.ForceSamsungLogin;


                Stream signedStream = packageStream;
                if (needsCertificates)
                {
                    // string selectedCertificate = _appSettings.Certificate;
                    //
                    // if (string.IsNullOrEmpty(selectedCertificate) || selectedCertificate == "Jelly2Sams (default)")
                    // {
                    progress?.Invoke("SamsungLogin".Localized());
                    ;
                    SamsungAuth auth = await SamsungLoginService.PerformSamsungLoginAsync(_browserService);
                    if (!string.IsNullOrEmpty(auth.AccessToken))
                    {
                        string email = auth.InputEmailID ?? "";
                        AuthorInfo authorInfo = new(
                            Name: email,
                            Email: email,
                            Password: email,
                            PrivilegeLevel: "Public"
                        );

                        SamsungCertificateCreator certCreator = new();
                        (var authorPfx, var distributorPfx, _) =
                            await certCreator.CreateCertificateAsync(authorInfo, auth, [tvDuid]);

                        progress?.Invoke("packageAndSign".Localized());
                        signedStream =
                            await TizenResigner.ResignPackageAsync(packageStream, authorPfx, distributorPfx);
                    }
                    else
                    {
                        await _dialogService.ShowErrorAsync("Failed to authenticate with Samsung account.");
                        return InstallResult.FailureResult("Auth failed.");
                    }
                    // }
                    // else
                    // {
                    //     // TODO: Load previous certificates
                    //     throw new InvalidOperationException("Previous certificate support not implemented yet.");
                    // }
                }
                else
                {
                }

                // TODO: Implement jellyfin config update
                // if (!string.IsNullOrEmpty(AppSettings.Default.JellyfinIP) &&
                //     !AppSettings.Default.ConfigUpdateMode.Contains("None"))
                // {
                //     string[] userIds = [];
                //
                //     if (AppSettings.Default.JellyfinUserId == "everyone" &&
                //         (AppSettings.Default.ConfigUpdateMode != "Server Settings"))
                //         userIds = [.. (await _jellyfinHelper.LoadJellyfinUsersAsync()).Select(u => u.Id)];
                //     else
                //         userIds = [AppSettings.Default.JellyfinUserId];
                //
                //     if (AppSettings.Default.ConfigUpdateMode.Contains("Server") ||
                //         AppSettings.Default.ConfigUpdateMode.Contains("Browser") ||
                //         AppSettings.Default.ConfigUpdateMode.Contains("All"))
                //     {
                //         await _jellyfinHelper.ApplyConfigAndResignPackageAsync(TizenCliPath, packageUrl,
                //             PackageCertificate, userIds);
                //     }
                //
                //
                //     if (AppSettings.Default.ConfigUpdateMode.Contains("User") ||
                //         AppSettings.Default.ConfigUpdateMode.Contains("All"))
                //     {
                //         await _jellyfinHelper.UpdateJellyfinUsersAsync(userIds);
                //     }
                // }

                progress?.Invoke("InstallingPackage".Localized());
                
                // Since we are always creating a new certificate, we need to remove previous installations
                string appId = await FindPackageId(signedStream);
                string appList = await sdbClient.ShellCommandAsync("0 vd_applist");
                bool needsUninstall = appList.Contains(appId);

                if (needsUninstall)
                    await sdbClient.ShellCommandAsync($"0 vd_appuninstall {appId}");

                string cmd = $"/home/owner/share/tmp/sdk_tools/tmp/app.wgt";
                await sdbClient.PushAsync(signedStream, cmd);

                string installOutput = await sdbClient.ShellCommandAsync($"0 vd_appinstall {appId} {cmd}");

                if (!installOutput.Contains("failed", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Invoke("InstallationSuccessful".Localized());
                    return InstallResult.SuccessResult();
                }

                progress?.Invoke("InstallationFailed".Localized());
                return InstallResult.FailureResult($"Output: {installOutput}");
            }
            catch (Exception ex)
            {
                progress?.Invoke($"Installation error: {ex.Message}");
                return InstallResult.FailureResult(ex.Message);
            }
        }

        private async Task<string> FindPackageId(Stream packageStream)
        {
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);

            ZipArchiveEntry? configEntry = archive.GetEntry("config.xml");
            ZipArchiveEntry? manifestEntry = archive.GetEntry("tizen-manifest.xml");
            bool isWgt = configEntry is not null;

            ZipArchiveEntry? targetEntry = isWgt ? configEntry : manifestEntry;

            if (targetEntry is null)
            {
                throw new Exception("Invalid App. No target entry found");
            }

            string xmlText;
            await using (Stream stream = targetEntry.Open())
            using (var sr = new StreamReader(stream))
            {
                xmlText = await sr.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(xmlText))
                throw new Exception("Invalid App. Could not read xml entry");

            XDocument doc;
            try
            {
                doc = XDocument.Parse(xmlText);
            }
            catch
            {
                throw new Exception("Invalid App. Could not read xml entry");
            }

            string? packageId = null;

            if (!isWgt)
            {
                XElement? root = doc.Root;
                packageId = root?.Attribute("package")?.Value;
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    XElement? manifestElem = doc.Descendants().FirstOrDefault(e =>
                        string.Equals(e.Name.LocalName, "manifest", StringComparison.OrdinalIgnoreCase));
                    packageId = manifestElem?.Attribute("package")?.Value;
                }
            }
            else
            {
                XElement? applicationElem = doc
                    .Descendants()
                    .FirstOrDefault(e =>
                        string.Equals(e.Name.LocalName, "application", StringComparison.OrdinalIgnoreCase));

                if (applicationElem is not null)
                {
                    packageId = applicationElem.Attribute("id")?.Value;
                }

                if (string.IsNullOrWhiteSpace(packageId))
                {
                    XElement? widgetElem = doc.Root;
                    if (widgetElem is not null)
                    {
                        string? idAttr = widgetElem.Attribute("id")?.Value;
                        if (!string.IsNullOrWhiteSpace(idAttr))
                            packageId = idAttr;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(packageId))
                throw new Exception("Invalid App. Could not find package ID");

            packageStream.Seek(0, SeekOrigin.Begin);
            return packageId.Trim();
        }

        public async Task<string?> GetTvNameAsync(string ip)
        {
            try
            {
                await using var sdbClient = new SdbTcpDevice(IPAddress.Parse(ip));
                await sdbClient.ConnectAsync();
                return sdbClient.DeviceId.Split("::")[1];
            }
            catch
            {
                return "";
            }
        }

        public string GetTvNameAsync(SdbTcpDevice sdbClient)
        {
            return sdbClient.DeviceId.Split("::")[1];
        }

        private async Task<string> FetchTizenOsAsync(SdbTcpDevice sdbClient)
        {
            var output = await sdbClient.CapabilityAsync();
            return output.GetValueOrDefault("platform_version", "").Trim();
        }

        private async Task<string> GetTvDuidAsync(SdbTcpDevice sdbClient)
        {
            string output = await sdbClient.ShellCommandAsync("0 getduid");
            var result = string.IsNullOrWhiteSpace(output.Trim())
                ? await sdbClient.ShellCommandAsync("/opt/etc/duid-gadget 2 2> /dev/null")
                : output;

            return result.Trim();
        }

        private void UpdateCertificateManager(string p12Location, string p12Password, string profileName)
        {
            if (string.IsNullOrEmpty(TizenDataPath))
                throw new Exception("Tizen data path is not set.");

            XElement profile = new XElement("profile",
                new XAttribute("name", profileName),
                new XElement("profileitem",
                    new XAttribute("ca", ""),
                    new XAttribute("distributor", "0"),
                    new XAttribute("key", Path.Combine(p12Location, "author.p12")),
                    new XAttribute("password", p12Password),
                    new XAttribute("rootca", "")
                ),
                new XElement("profileitem",
                    new XAttribute("ca", ""),
                    new XAttribute("distributor", "1"),
                    new XAttribute("key", Path.Combine(p12Location, "distributor.p12")),
                    new XAttribute("password", p12Password),
                    new XAttribute("rootca", "")
                )
            );

            XDocument doc;
            string dir = Path.GetDirectoryName(TizenDataPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(TizenDataPath))
            {
                doc = new XDocument(new XElement("profiles", profile));
            }
            else
            {
                doc = XDocument.Load(TizenDataPath);
                var root = doc.Element("profiles")!;
                var existing = root.Elements("profile").FirstOrDefault(p => p.Attribute("name")?.Value == profileName);
                if (existing == null) root.Add(profile);
                else existing.ReplaceWith(profile);
            }

            doc.Save(TizenDataPath);
        }

        private static string? FindTizenRoot()
        {
            foreach (var basePath in PossibleTizenPaths)
            {
                if (string.IsNullOrEmpty(basePath))
                    continue;

                string tizenExecutable = OperatingSystem.IsWindows() ? "tizen.bat" : "tizen";

                var possiblePath = Path.Combine(basePath, "tools", "ide", "bin", tizenExecutable);
                if (File.Exists(possiblePath))
                    return basePath;
            }

            return null;
        }

        public async Task<bool> InstallSamsungCertificateExtensionAsync(string installPath,
            InstallingWindow installingWindow)
        {
            string certManagerExe = OperatingSystem.IsWindows() ? "certificate-manager.exe" : "certificate-manager.bin";
            string[] possiblePaths =
            {
                Path.Combine(installPath, "tools", "certificate-manager", certManagerExe),
                Path.Combine(installPath, "certificate-manager", certManagerExe)
            };

            // Already installed?
            if (possiblePaths.Any(File.Exists))
                return true;

            string packageManagerExe =
                OperatingSystem.IsWindows() ? "package-manager-cli.exe" : "package-manager-cli.bin";
            string packageManagerPath = Path.Combine(installPath, "package-manager", packageManagerExe);

            if (!File.Exists(packageManagerPath))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    installingWindow.ViewModel.SetStatusText(
                        "Package manager CLI not found. Please ensure Tizen Studio is properly installed.")
                );
                return false;
            }

            await EnsureTizenExtensionsEnabledAsync(installPath, packageManagerPath, installingWindow);

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // ---- Certificate Manager ----
                    installingWindow.ViewModel.SetStatusText("Installing Certificate Manager...");
                    var certProcessInfo = new ProcessStartInfo
                    {
                        FileName = packageManagerPath,
                        Arguments = "install \"Certificate-Manager\" --accept-license",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = installPath
                    };

                    using var certProcess = Process.Start(certProcessInfo);
                    await certProcess.WaitForExitAsync();
                    if (certProcess.ExitCode != 0)
                        return false;

                    // ---- Cert Add-On ----
                    installingWindow.ViewModel.SetStatusText("Installing Certificate Add-On...");
                    var addOnProcessInfo = new ProcessStartInfo
                    {
                        FileName = packageManagerPath,
                        Arguments = "install \"cert-add-on\" --accept-license",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = installPath
                    };

                    using var addOnProcess = Process.Start(addOnProcessInfo);
                    await addOnProcess.WaitForExitAsync();
                    if (addOnProcess.ExitCode != 0)
                        return false;
                }
                else
                {
                    // Linux/macOS CLI-based installation
                    installingWindow.ViewModel.SetStatusText("Installing Certificate Manager...");
                    await _processHelper.RunCommandAsync(packageManagerPath,
                        "install Certificate-Manager --accept-license");

                    installingWindow.ViewModel.SetStatusText("Installing Certificate Add-On...");
                    //await _processHelper.RunCommandAsyncEx("pkexec",new[] { packageManagerPath, "install", "cert-add-on", "--accept-license" });
                    await _processHelper.RunPrivilegedCommandAsync(packageManagerPath,
                        new[] { "install", "cert-add-on", "--accept-license" });
                }

                // Verify installation
                if (possiblePaths.Any(File.Exists))
                    return true;

                await Dispatcher.UIThread.InvokeAsync(() =>
                    installingWindow.ViewModel.SetStatusText(
                        "Installation completed but certificate manager executable not found.")
                );
                return false;
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    installingWindow.ViewModel.SetStatusText(
                        $"Samsung Certificate Extension installation failed: {ex.Message}")
                );
                return false;
            }
        }

        public async Task EnsureTizenExtensionsEnabledAsync(string installPath, string packageManagerPath,
            InstallingWindow installingWindow)
        {
            installingWindow.ViewModel.SetStatusText("CheckingPackageManagerList".Localized());

            var result = OperatingSystem.IsWindows()
                ? await _processHelper.RunElevatedAndCaptureOutputAsync(packageManagerPath, "extra --list --detail",
                    installPath)
                : await _processHelper.RunCommandAsync(packageManagerPath, "extra --list --detail", installPath);

            string output = result?.Output ?? string.Empty;

            if (string.IsNullOrWhiteSpace(output))
            {
                installingWindow.ViewModel.SetStatusText("Failed to retrieve extension list.");
                throw new InvalidOperationException("Failed to get extension output.");
            }

            var extensions = _fileHelper.ParseExtensions(output);
            var targets = new[] { "Samsung Certificate Extension", "Samsung Tizen TV SDK" };

            foreach (var target in targets)
            {
                var ext = extensions.FirstOrDefault(e => e.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
                if (ext == null)
                {
                    installingWindow.ViewModel.SetStatusText($"Extension '{target}' not found.");
                    continue;
                }

                if (ext.Activated)
                {
                    installingWindow.ViewModel.SetStatusText($"Extension '{target}' already active.");
                }
                else
                {
                    installingWindow.ViewModel.SetStatusText($"Activating extension: {target}...");

                    var args = $"extra -act {ext.Index}";

                    var activationResult = OperatingSystem.IsWindows()
                        ? await _processHelper.RunElevatedAndCaptureOutputAsync(packageManagerPath, args, installPath)
                        : await _processHelper.RunCommandAsync(packageManagerPath, args, installPath);

                    string activationOutput = activationResult?.Output ?? string.Empty;


                    if (activationOutput.Contains("activated", StringComparison.OrdinalIgnoreCase) ||
                        activationOutput.Contains("success", StringComparison.OrdinalIgnoreCase))
                    {
                        installingWindow.ViewModel.SetStatusText($"Activated: {target}");
                    }
                    else
                    {
                        installingWindow.ViewModel.SetStatusText($"Failed to activate {target}.");
                        throw new InvalidOperationException($"Failed to activate extension {target}. Output: {result}");
                    }
                }
            }
        }
    }
}