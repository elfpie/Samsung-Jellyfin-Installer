using System.Threading.Tasks;
using AvaloniaXplat.Extensions;

namespace AvaloniaXplat.Services
{
    public interface ITizenCertificateService
    {
        Task<(string p12Location, string p12Password)> GenerateProfileAsync(string duid, string accessToken, string userId, string userEmail, string outputPath, string jarPath, ProgressCallback? progress = null);
    }
}
