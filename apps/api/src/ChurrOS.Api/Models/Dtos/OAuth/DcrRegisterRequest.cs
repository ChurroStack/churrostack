using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.OAuth
{
    // RFC 7591 mandates these exact snake_case field names on the wire, regardless of the
    // app's global camelCase JSON convention (Utils/JsonSettings.cs) -- explicit
    // [JsonPropertyName] always wins over the configured naming policy.
    public class DcrRegisterRequest
    {
        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("redirect_uris")]
        public string[] RedirectUris { get; set; } = [];

        [JsonPropertyName("grant_types")]
        public string[]? GrantTypes { get; set; }

        [JsonPropertyName("token_endpoint_auth_method")]
        public string? TokenEndpointAuthMethod { get; set; }
    }
}
