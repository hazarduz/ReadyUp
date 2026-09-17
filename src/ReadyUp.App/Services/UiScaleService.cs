using ReadyUp.Core.Interfaces;

namespace ReadyUp.App.Services;

/// <summary>
/// Owns the app-wide UI scale factor (0.75-1.5), applied by each shell
/// window via a LayoutTransform on its root Grid. Using LayoutTransform
/// (not RenderTransform) means WPF re-measures at the new size instead of
/// just stretching pixels, so it composes correctly with per-monitor DPI
/// scaling and stays crisp at any refresh rate.
/// </summary>
public sealed class UiScaleService
{
    private readonly ISettingsService _settingsService;

    public event EventHandler<double>? ScaleChanged;

    public double CurrentScale => _settingsService.Current.UiScale;

    public UiScaleService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += (_, settings) => ScaleChanged?.Invoke(this, settings.UiScale);
    }

    public async Task SetScaleAsync(double scale)
    {
        var clamped = Math.Clamp(scale, 0.75, 1.5);
        await _settingsService.UpdateAsync(s => s.UiScale = clamped).ConfigureAwait(false);
    }
}
