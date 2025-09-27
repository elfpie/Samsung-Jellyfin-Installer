using System.IO;
using System.Threading.Tasks;
using AvaloniaXplat.Extensions;
using AvaloniaXplat.Models;

namespace AvaloniaXplat.Services
{
    public interface ITizenInstallerService
    {
        string TizenCliPath { get; }
        Task<Stream> DownloadPackageAsync(string downloadUrl);
        Task<InstallResult> InstallPackageAsync(Stream packageStream, string tvIpAddress, ProgressCallback? progress = null);
        Task<string?> GetTvNameAsync(string tvIpAddress);
        Task<bool> ConnectToTvAsync(string tvIpAddress);
    }
}
