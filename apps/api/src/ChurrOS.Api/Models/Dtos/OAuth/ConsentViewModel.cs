namespace ChurrOS.Api.Models.Dtos.OAuth
{
    public sealed class ConsentViewModel
    {
        public required string ApplicationName { get; init; }
        public required string ClientId { get; init; }
        public required string RedirectHost { get; init; }
        public required bool IsLoopbackRedirect { get; init; }
        public required string UserName { get; init; }
        public required IReadOnlyList<ConsentScopeViewModel> Scopes { get; init; }

        // The original OAuth request's parameters, re-emitted as hidden form fields so Accept()/Deny()
        // see the exact same request OpenIddict already parsed for this consent prompt.
        public required IReadOnlyList<KeyValuePair<string, string>> Parameters { get; init; }

        // Belt-and-braces: kept on the form's action in addition to the hidden fields above, in case
        // OpenIddict's POST request extraction in this version doesn't merge the query string.
        public required string QueryString { get; init; }
    }

    public sealed class ConsentScopeViewModel
    {
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public string? Description { get; init; }
    }
}
