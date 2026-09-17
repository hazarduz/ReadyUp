using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Scanning;

/// <summary>
/// Fans multiple <see cref="IGameScanner"/> sources out in parallel and
/// yields deduplicated <see cref="ScanResult"/>s (by normalized executable
/// path) as soon as each one is found, so the UI can populate the library
/// incrementally instead of waiting for the slowest source to finish.
/// </summary>
public sealed class CompositeGameScanner
{
    private readonly IReadOnlyList<IGameScanner> _scanners;

    public CompositeGameScanner(IEnumerable<IGameScanner> scanners)
    {
        _scanners = scanners.ToList();
    }

    public async IAsyncEnumerable<ScanResult> ScanAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<ScanResult>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var producers = _scanners.Select(scanner => ProduceAsync(scanner, channel.Writer, ct)).ToArray();

        _ = Task.WhenAll(producers).ContinueWith(
            _ => channel.Writer.TryComplete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        await foreach (var result in channel.Reader.ReadAllAsync(ct))
        {
            var normalized = Path.GetFullPath(result.ExecutablePath).TrimEnd(Path.DirectorySeparatorChar);
            if (seen.Add(normalized))
            {
                yield return result;
            }
        }
    }

    private static async Task ProduceAsync(IGameScanner scanner, ChannelWriter<ScanResult> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var result in scanner.ScanAsync(ct).WithCancellation(ct))
            {
                await writer.WriteAsync(result, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the overall scan is cancelled; other producers keep running independently.
        }
        catch (Exception)
        {
            // A single failing source (e.g. a locked drive, a denied registry key) must not abort the others.
        }
    }
}
