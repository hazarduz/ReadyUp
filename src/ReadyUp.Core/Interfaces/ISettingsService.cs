using ReadyUp.Core.Models;

namespace ReadyUp.Core.Interfaces;

/// <summary>Loads/persists <see cref="AppSettings"/> and notifies subscribers of changes.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    event EventHandler<AppSettings>? SettingsChanged;

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Mutates <see cref="Current"/> via <paramref name="mutate"/> then persists and raises <see cref="SettingsChanged"/>.</summary>
    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default);
}
