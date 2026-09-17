using System.Text.Json;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Data;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public JsonSettingsService(string? settingsPath = null)
    {
        AppPaths.EnsureCreated();
        _path = settingsPath ?? AppPaths.SettingsPath;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                Current = new AppSettings();
                return;
            }

            await using var stream = File.OpenRead(_path);
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: ct).ConfigureAwait(false)
                       ?? new AppSettings();
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
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
    {
        mutate(Current);
        Current.UiScale = Math.Clamp(Current.UiScale, 0.75, 1.5);
        Current.GamepadDeadzone = Math.Clamp(Current.GamepadDeadzone, 0.0, 0.9);

        await SaveAsync(ct).ConfigureAwait(false);
        SettingsChanged?.Invoke(this, Current);
    }
}
