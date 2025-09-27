using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaXplat.Models;

namespace AvaloniaXplat.Services
{
    public interface INetworkService
    {
        Task<IEnumerable<NetworkDevice>> GetLocalTizenAddresses(CancellationToken cancellationToken = default, bool virtualScan = false);
        Task<NetworkDevice?> ValidateManualTizenAddress(string ip, CancellationToken cancellationToken = default);
        IEnumerable<IPAddress> GetRelevantLocalIPs(bool virtualScan = false);
        Task<bool> IsPortOpenAsync(string ip, int port, CancellationToken ct);
        string GetLocalIPAddress();
        string InvertIPAddress(string ipAddress);
    }
}
