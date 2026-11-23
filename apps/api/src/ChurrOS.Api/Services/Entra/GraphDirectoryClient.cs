using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ChurrOS.Api.Services.Entra
{
    /// <summary>
    /// Minimal Microsoft Graph client (users, groups, transitive group members) using the OAuth client-credentials flow
    /// with the app registration configured in <c>ExternalProviders:Microsoft</c>. Registered as a typed HttpClient so it
    /// inherits the standard resilience handler (429/5xx retry, Retry-After) from ServiceDefaults.
    /// Instances are transient: the access token is cached per instance (one sync run) and refreshed before expiry.
    /// Required Graph application permissions (admin consent): User.Read.All, GroupMember.Read.All.
    /// </summary>
    public class GraphDirectoryClient
    {
        private const string GraphBase = "https://graph.microsoft.com/v1.0";
        private static readonly string[] BaseUserProperties = { "id", "displayName", "mail", "userPrincipalName", "accountEnabled" };

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GraphDirectoryClient> _logger;
        private string? _accessToken;
        private DateTimeOffset _accessTokenExpiresAt;
        private string? _tenantId;

        public GraphDirectoryClient(HttpClient httpClient, IConfiguration configuration, ILogger<GraphDirectoryClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public static bool IsConfigured(IConfiguration configuration) =>
            !string.IsNullOrWhiteSpace(configuration["ExternalProviders:Microsoft:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["ExternalProviders:Microsoft:ClientSecret"]);


        public void UseTenant(string tenantId)
        {
            _tenantId = tenantId;
            _accessToken = null;
        }

        public async IAsyncEnumerable<EntraUser> GetUsersAsync(IEnumerable<string> extraProperties, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var properties = BaseUserProperties.Concat(extraProperties).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var url = $"{GraphBase}/users?$top=500&$select={string.Join(",", properties)}";

            await foreach (var item in GetPagedAsync(url, cancellationToken))
            {
                var raw = item.EnumerateObject().ToDictionary(o => o.Name, o => o.Value, StringComparer.OrdinalIgnoreCase);
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                    values[property] = raw.TryGetValue(property, out var element) ? Scalar(element) : null;

                var accountEnabled = !(item.TryGetProperty("accountEnabled", out var enabled) && enabled.ValueKind == JsonValueKind.False);
                yield return new EntraUser(values["id"]!, values["displayName"], values["mail"], values["userPrincipalName"], accountEnabled, values);
            }
        }

        public async IAsyncEnumerable<(string Id, string? DisplayName)> GetGroupsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in GetPagedAsync($"{GraphBase}/groups?$top=999&$select=id,displayName", cancellationToken))
                yield return (item.GetProperty("id").GetString()!, item.TryGetProperty("displayName", out var name) ? Scalar(name) : null);
        }

        /// <summary>Transitive user members (nested groups flattened). Unlike $expand=members this is not capped at 20.</summary>
        public async IAsyncEnumerable<string> GetGroupUserMemberIdsAsync(string groupId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var url = $"{GraphBase}/groups/{Uri.EscapeDataString(groupId)}/transitiveMembers/microsoft.graph.user?$top=999&$select=id";
            await foreach (var item in GetPagedAsync(url, cancellationToken))
                yield return item.GetProperty("id").GetString()!;
        }

        private async IAsyncEnumerable<JsonElement> GetPagedAsync(string firstUrl, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? url = firstUrl;
            while (url is not null)
            {
                using var document = await GetJsonAsync(url, cancellationToken);
                if (document.RootElement.TryGetProperty("value", out var values))
                {
                    foreach (var value in values.EnumerateArray())
                        yield return value.Clone();
                }

                url = document.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
            }
        }

        private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            await EnsureTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractError(body) ?? response.ReasonPhrase ?? "request failed";
                _logger.LogWarning("[EntraSync] graph call failed status={Status} path={Path} error={Error}", (int)response.StatusCode, new Uri(url).AbsolutePath, message);
                throw new EntraSyncException($"Microsoft Graph returned {(int)response.StatusCode}: {message}");
            }

            return JsonDocument.Parse(body);
        }

        private async Task EnsureTokenAsync(CancellationToken cancellationToken)
        {
            if (_accessToken is not null && _accessTokenExpiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(5))
                return;

            if (string.IsNullOrWhiteSpace(_tenantId))
                throw new EntraSyncException("Tenant ID is not configured.");
            if (!IsConfigured(_configuration))
                throw new EntraSyncException("The Microsoft app registration (ExternalProviders:Microsoft ClientId/ClientSecret) is not configured.");

            var tokenUrl = $"https://login.microsoftonline.com/{Uri.EscapeDataString(_tenantId)}/oauth2/v2.0/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _configuration["ExternalProviders:Microsoft:ClientId"]!,
                    ["client_secret"] = _configuration["ExternalProviders:Microsoft:ClientSecret"]!,
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["grant_type"] = "client_credentials"
                })
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractError(body) ?? response.ReasonPhrase ?? "token request failed";
                _logger.LogWarning("[EntraSync] token request failed status={Status} error={Error}", (int)response.StatusCode, message);
                throw new EntraSyncException($"Entra authentication failed: {message}");
            }

            using var document = JsonDocument.Parse(body);
            _accessToken = document.RootElement.GetProperty("access_token").GetString();
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            _logger.LogInformation("[EntraSync] token acquired expiresInSeconds={ExpiresIn}", expiresIn);
        }

        /// <summary>Compact AAD (error_description) / Graph (error.message) error text; never includes tokens.</summary>
        private static string? ExtractError(string body)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                string? message = null;
                if (root.TryGetProperty("error_description", out var description))
                    message = description.GetString();
                else if (root.TryGetProperty("error", out var error))
                    message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m) ? m.GetString() : error.GetString();

                if (string.IsNullOrWhiteSpace(message)) return null;
                var firstLine = message.Split('\r', '\n')[0].Trim();
                return firstLine.Length > 300 ? firstLine[..300] : firstLine;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Scalar(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => null
        };
    }
}
