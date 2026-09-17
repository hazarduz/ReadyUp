using System.Text.Json;
using ReadyUp.Core.Interfaces;

namespace ReadyUp.Data;

/// <summary>
/// Persists a directory-path → fingerprint map so re-scans can skip folders
/// whose contents haven't changed since the last scan (fingerprint is
/// typically a hash of file count + max last-write-time, computed by the caller).
/// </summary>
public sealed class JsonScanCache : IScanCache
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, string> _fingerprints = new(StringComparer.OrdinalIgnoreCase);

    public JsonScanCache(string? path = null)
    {
        AppPaths.EnsureCreated();
        _path = path ?? AppPaths.ScanCachePath;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                _fingerprints = new(StringComparer.OrdinalIgnoreCase);
                return;
            }

            await using var stream = File.OpenRead(_path);
            _fingerprints = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: ct)
                                .ConfigureAwait(false)
                            ?? new(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var stream = File.Create(_path);
            await JsonSerializer.SerializeAsync(stream, _fingerprints, cancellationToken: ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool IsUnchanged(string directoryPath, string fingerprint)
        => _fingerprints.TryGetValue(directoryPath, out var stored) && stored == fingerprint;

    public void Record(string directoryPath, string fingerprint)
        => _fingerprints[directoryPath] = fingerprint;
}
