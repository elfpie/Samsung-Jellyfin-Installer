using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaXplat.SdbClient;

public interface ISdbFrameTransport : IAsyncDisposable
{
    Task WriteFrameAsync(SdbFrame frame, CancellationToken ct = default);
    Task<SdbFrame> ReadFrameAsync(CancellationToken ct = default);
    EndPoint RemoteEndPoint { get; }
}
