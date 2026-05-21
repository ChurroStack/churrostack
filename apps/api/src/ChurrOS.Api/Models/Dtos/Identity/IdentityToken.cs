namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class IdentityToken
    {
        public string LoginHint { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string Scope { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }

        public IdentityToken(string loginHint, string accessToken, string refreshToken, string scope, DateTimeOffset expiresAt)
        {
            LoginHint = loginHint;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            Scope = scope;
            ExpiresAt = expiresAt;
        }
    }
}
