using ChurrOS.Api.Utils;
using LazyCache;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ChurrOS.Api.Middlewares
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";
        private readonly IConfiguration _configuration;
        private readonly IAppCache _appCache;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration, IAppCache appCache, IServiceScopeFactory serviceScopeFactory) : base(options, logger, encoder)
        {
            _configuration = configuration;
            _appCache = appCache;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                return AuthenticateResult.NoResult();
            }
            var apiKey = authHeader.Split(' ').Last();
            var hashedKey = apiKey.GetSha256Hash();
            var authResult = await _appCache.GetOrAdd($"api_key_auth:{Convert.ToHexString(hashedKey)}", async ctx =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<Data.ChurrosDbContext>();
                var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
                var isOpen = conn.State == System.Data.ConnectionState.Open;
                if (!isOpen)
                    await conn.OpenAsync();
                try
                {
                    var dt = new DataTable();
                    dt.Columns.Add("account_id", typeof(long));
                    dt.Columns.Add("identity_id", typeof(long));
                    dt.Columns.Add("name", typeof(string));

                    await using var cmd = new NpgsqlCommand("""
                        SELECT a.account_id, a.identity_id, u.name
                        FROM cs.api_key a
                        LEFT JOIN cs.identity u ON a.identity_id = u.id
                        WHERE a.value = @value
                        """, conn);
                    cmd.Parameters.Add(new NpgsqlParameter("value", hashedKey));

                    await using var reader = await cmd.ExecuteReaderAsync();
                    dt.Load(reader);

                    if (dt.Rows.Count == 0)
                    {
                        return (null, null);
                    }

                    return ((long?)dt.Rows[0]["account_id"], (string?)dt.Rows[0]["name"]);
                }
                finally
                {
                    if (!isOpen)
                    {
                        conn.Close();
                    }
                }
            });

            if (authResult.Item1 is null || authResult.Item2 is null)
            {
                return AuthenticateResult.NoResult();
            }

            Context.Items["AccountId"] = authResult.Item1;

            var name = authResult.Item2;
            var identity = new ClaimsIdentity([
                new Claim("sub", name),
                new Claim(_configuration["Authentication:NameClaim"] ?? "preferred_username", name)
            ], Scheme.Name, _configuration["Authentication:NameClaim"] ?? "preferred_username", _configuration["Authentication:RoleClaim"] ?? "role");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
