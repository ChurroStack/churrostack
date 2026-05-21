using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace ChurrunKubernetes.Services
{
    public class HmacAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HmacAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        public HmacAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Environment-Name", out var environmentName))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing X-Environment-Name header"));
            }
            if (!Request.Headers.TryGetValue("X-Signature", out var signature))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing X-Signature header"));
            }
            if (!Request.Headers.TryGetValue("X-Timestamp", out var timestsamp))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing X-Timestamp header"));
            }

            var configuration = Request.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            using var hmac = new HMACSHA256(Convert.FromBase64String(configuration["EncryptionKey"]!));
            var calcSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{BuildCanonicalUrl(Request)}:{environmentName}:{timestsamp}"))).ToLower();

            if (!string.Equals(signature, calcSignature, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid signature"));
            }

            var claims = new[] { new Claim(ClaimTypes.Name, environmentName!) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        private static string BuildCanonicalUrl(HttpRequest request)
        {
            var path = request.Path.Value!.ToLowerInvariant();

            var query = request.Query
                .OrderBy(q => q.Key)
                .Select(q => $"{q.Key.ToLowerInvariant()}={q.Value}")
                .ToArray();

            return path + "\n" + string.Join("&", query);
        }
    }
}
