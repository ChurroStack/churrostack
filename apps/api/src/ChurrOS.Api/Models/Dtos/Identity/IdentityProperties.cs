namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class IdentityProperties
    {
        public IDictionary<string, string> Claims { get; set; }
        public IDictionary<string, IdentityToken> Tokens { get; set; }
        public IDictionary<string, object> Metadata { get; set; }

        public IdentityProperties(IDictionary<string, string> claims, IDictionary<string, IdentityToken> tokens, IDictionary<string, object> metadata)
        {
            Claims = claims ?? new Dictionary<string, string>();
            Tokens = tokens ?? new Dictionary<string, IdentityToken>();
            Metadata = metadata ?? new Dictionary<string, object>();
        }
    }
}
