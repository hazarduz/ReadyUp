using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyUp.Core.Interfaces;
using ReadyUp.Core.Models;

namespace ReadyUp.Metadata;

/// <summary>
/// Optional metadata provider backed by IGDB (api.igdb.com), used for
/// descriptions and developer/publisher enrichment. Authenticates via the
/// Twitch OAuth2 client-credentials flow using a user-supplied client
/// id/secret pair (Settings screen); returns no results when unset.
/// </summary>
public sealed class IgdbMetadataProvider : IMetadataProvider
{
    private static readonly Uri TokenUri = new("https://id.twitch.tv/oauth2/token");
    private static readonly Uri GamesUri = new("https://api.igdb.com/v4/games");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly Func<(string? ClientId, string? ClientSecret)> _credentialsAccessor;

    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiresAtUtc = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    public string Name => "IGDB";

    public bool IsAvailable
    {
        get
        {
            var (clientId, clientSecret) = _credentialsAccessor();
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }
    }

    public IgdbMetadataProvider(HttpClient http, Func<(string? ClientId, string? ClientSecret)> credentialsAccessor)
    {
        _http = http;
        _credentialsAccessor = credentialsAccessor;
    }

    public async Task<MetadataRecord?> FetchAsync(Game game, CancellationToken ct = default)
    {
        var (clientId, clientSecret) = _credentialsAccessor();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)) return null;

        var token = await GetAccessTokenAsync(clientId, clientSecret, ct).ConfigureAwait(false);
        if (token is null) return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, GamesUri)
        {
            Content = new StringContent(
                $"""search "{EscapeQuery(game.Title)}"; fields name,summary,involved_companies.company.name,involved_companies.developer,involved_companies.publisher; limit 5;""",
                Encoding.UTF8),
        };
        request.Headers.Add("Client-ID", clientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var results = await response.Content.ReadFromJsonAsync<List<IgdbGame>>(JsonOptions, ct).ConfigureAwait(false);
        var best = results?.FirstOrDefault();
        if (best is null) return null;

        var publisher = best.InvolvedCompanies?.FirstOrDefault(c => c.Publisher)?.Company?.Name;
        var developer = best.InvolvedCompanies?.FirstOrDefault(c => c.Developer)?.Company?.Name;

        return new MetadataRecord
        {
            ProviderName = Name,
            Confidence = 0.7,
            Title = best.Name,
            Description = best.Summary,
            Publisher = publisher,
            Developer = developer,
        };
    }

    private async Task<string?> GetAccessTokenAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAtUtc)
        {
            return _cachedAccessToken;
        }

        await _tokenGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAtUtc)
            {
                return _cachedAccessToken;
            }

            var query = $"?client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(clientSecret)}&grant_type=client_credentials";
            using var response = await _http.PostAsync(new Uri(TokenUri + query), content: null, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var token = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(JsonOptions, ct).ConfigureAwait(false);
            if (token is null) return null;

            _cachedAccessToken = token.AccessToken;
            _tokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
            return _cachedAccessToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private static string EscapeQuery(string value) => value.Replace("\"", "\\\"");

    private sealed class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class IgdbGame
    {
        public string? Name { get; set; }
        public string? Summary { get; set; }

        [JsonPropertyName("involved_companies")]
        public List<IgdbInvolvedCompany>? InvolvedCompanies { get; set; }
    }

    private sealed class IgdbInvolvedCompany
    {
        public IgdbCompany? Company { get; set; }
        public bool Developer { get; set; }
        public bool Publisher { get; set; }
    }

    private sealed class IgdbCompany
    {
        public string? Name { get; set; }
    }
}
