using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.OAuth
{
    public class DcrRegisterResponse
    {
        [JsonPropertyName("client_id")]
        public required string ClientId { get; set; }

        [JsonPropertyName("client_id_issued_at")]
        public long ClientIdIssuedAt { get; set; }

        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("redirect_uris")]
        public required string[] RedirectUris { get; set; }

        [JsonPropertyName("grant_types")]
        public required string[] GrantTypes { get; set; }

        [JsonPropertyName("response_types")]
        public string[] ResponseTypes { get; set; } = ["code"];

        [JsonPropertyName("token_endpoint_auth_method")]
        public string TokenEndpointAuthMethod { get; set; } = "none";
    }
}
