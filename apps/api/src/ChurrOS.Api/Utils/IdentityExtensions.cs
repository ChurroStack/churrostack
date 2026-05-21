using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace ChurrOS.Api.Utils
{
    public static class IdentityExtensions
    {
        public static ClaimsIdentity ToClaimIdentity(this string identityName, string? displayName = null, string? language = null, string[]? audience = null, string[]? groups = null,
            string? company = null, string? location = null, string? timezone = null, IDictionary<string, string>? properties = null, IDictionary<string, object>? metadata = null)
        {
            var list = new List<Claim>() {
                new Claim(ClaimTypes.NameIdentifier, identityName),
                new Claim(ClaimTypes.Name, identityName),
                new Claim(ClaimTypes.Upn, identityName),
            };
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                list.Add(new Claim(ClaimTypes.GivenName, displayName));
            }
            ;
            if (!string.IsNullOrWhiteSpace(language))
            {
                list.Add(new Claim("locale", language));
            }
            if (audience != null)
            {
                foreach (var item in audience)
                    list.Add(new Claim("audience", item));
            }
            if (groups != null)
            {
                foreach (var item in groups)
                    list.Add(new Claim("group", item));
            }
            if (!string.IsNullOrWhiteSpace(company))
            {
                list.Add(new Claim("company", company));
            }
            if (!string.IsNullOrWhiteSpace(location))
            {
                list.Add(new Claim(ClaimTypes.Locality, location));
            }
            if (!string.IsNullOrWhiteSpace(timezone))
            {
                list.Add(new Claim("zoneinfo", timezone));
            }
            return new ClaimsIdentity(list, "api");
        }

        public static ClaimsIdentity ProcessClaims(this IEnumerable<Claim> claims, string nameClaimType, string roleClaimType, TokenValidationParameters tokenValidationParameters, string? authenticationType)
        {
            var transformedClaims = new List<Claim>();
            var nameClaim = claims.FirstOrDefault(c => c.Type == nameClaimType);
            if (nameClaim is null)
            {
                nameClaim = claims.FirstOrDefault(c => c.Type == "azp");
                if (nameClaim is not null)
                {
                    nameClaimType = "azp";
                }
                else if (nameClaim is null)
                {
                    nameClaim = claims.FirstOrDefault(c => c.Type == "sub");
                    if (nameClaim is not null)
                    {
                        nameClaimType = "sub";
                    }
                }
            }
            if (nameClaim is not null)
            {
                transformedClaims.Add(new Claim(nameClaim.Type, nameClaim.Value.ToLowerInvariant()));
            }
            foreach (var claim in claims)
            {
                if (nameClaim is not null && claim.Type == nameClaim.Type)
                    continue;

                transformedClaims.Add(new Claim(claim.Type, claim.Value));
            }
            return new ClaimsIdentity(transformedClaims, authenticationType, nameClaimType, roleClaimType);
        }
    }
}
