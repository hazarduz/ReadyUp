using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Metadata;

/// <summary>
/// Art provider backed by the community SteamGridDB API
/// (https://www.steamgriddb.com/api/v2) — the same source the
/// Decky SteamGridDB plugin uses on Steam Deck. Requires a user-supplied
/// API key (Settings screen); returns no results when unset.
/// </summary>
public sealed class SteamGridDbArtProvider : IArtProvider
{
    private static readonly Uri BaseUri = new("https://www.steamgriddb.com/api/v2/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyAccessor;

    public string Name => "SteamGridDB";
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKeyAccessor());

    /// <param name="http">A shared HttpClient (base address not required; absolute URIs are used).</param>
    /// <param name="apiKeyAccessor">Reads the current API key from settings on every call, so a key entered mid-session is picked up immediately.</param>
    public SteamGridDbArtProvider(HttpClient http, Func<string?> apiKeyAccessor)
    {
        _http = http;
        _apiKeyAccessor = apiKeyAccessor;
    }

    public async Task<IReadOnlyList<ArtCandidate>> SearchAsync(Game game, ArtAssetType type, CancellationToken ct = default)
    {
        var apiKey = _apiKeyAccessor();
        if (string.IsNullOrWhiteSpace(apiKey)) return Array.Empty<ArtCandidate>();

        var gameId = await ResolveGameIdAsync(game.Title, apiKey, ct).ConfigureAwait(false);
        if (gameId is null) return Array.Empty<ArtCandidate>();

        var endpoint = type switch
        {
            ArtAssetType.Banner => $"grids/game/{gameId}?dimensions=460x215,920x430",
            ArtAssetType.BoxArt => $"grids/game/{gameId}?dimensions=600x900",
            ArtAssetType.Background => $"heroes/game/{gameId}",
            ArtAssetType.Icon => $"icons/game/{gameId}",
            _ => null,
        };
        if (endpoint is null) return Array.Empty<ArtCandidate>();

        using var request = CreateRequest(endpoint, apiKey);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return Array.Empty<ArtCandidate>();

        var payload = await response.Content
            .ReadFromJsonAsync<SteamGridDbListResponse>(JsonOptions, ct)
            .ConfigureAwait(false);
        if (payload?.Data is null) return Array.Empty<ArtCandidate>();

        return payload.Data
            .Select(item => new ArtCandidate
            {
                Type = type,
                ProviderName = Name,
                ThumbnailUrl = item.Thumb ?? item.Url ?? string.Empty,
                FullImageUrl = item.Url ?? string.Empty,
                WidthPixels = item.Width,
                HeightPixels = item.Height,
            })
            .Where(c => !string.IsNullOrEmpty(c.FullImageUrl))
            .ToList();
    }

    public async Task ApplyAsync(ArtCandidate candidate, string destinationPath, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(candidate.FullImageUrl, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var target = File.Create(destinationPath);
        await response.Content.CopyToAsync(target, ct).ConfigureAwait(false);
    }

    private async Task<int?> ResolveGameIdAsync(string title, string apiKey, CancellationToken ct)
    {
        using var request = CreateRequest($"search/autocomplete/{Uri.EscapeDataString(title)}", apiKey);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var payload = await response.Content
            .ReadFromJsonAsync<SteamGridDbSearchResponse>(JsonOptions, ct)
            .ConfigureAwait(false);
        return payload?.Data?.FirstOrDefault()?.Id;
    }

    private static HttpRequestMessage CreateRequest(string relativeUrl, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private sealed class SteamGridDbSearchResponse
    {
        public List<SteamGridDbGame>? Data { get; set; }
    }

    private sealed class SteamGridDbGame
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class SteamGridDbListResponse
    {
        public List<SteamGridDbImage>? Data { get; set; }
    }

    private sealed class SteamGridDbImage
    {
        public string? Url { get; set; }
        public string? Thumb { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
