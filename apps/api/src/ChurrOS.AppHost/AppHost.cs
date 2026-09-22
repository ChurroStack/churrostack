using ChurrOS.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
var externalServices = builder.Configuration.GetSection("ExternalServices");

var cacheConnectionString = builder.AddConnectionString("Cache");
var databaseConnectionString = builder.AddConnectionString("Database");

var churrosApi = builder.AddProject<Projects.ChurrOS_Api>("churros-api")
    .WithConfiguration("ExternalProviders")
    .WithConfiguration("Owners")
    .WithConfiguration("CreateAccount")
    .WithConfiguration("BaseUrl")
    .WithConfiguration("ClientSecret")
    .WithConfiguration("Tunnel")
    .WithReference(cacheConnectionString)
    .WithReference(databaseConnectionString);

bool useExternalAddress = false;
if (bool.TryParse(builder.Configuration["UseExternalAddress"], out var boolValue))
{
    useExternalAddress = boolValue;
}

builder.AddYarp("ingress", useExternalAddress: useExternalAddress)
       .WithEndpoint(8000, scheme: "http")
       .WithEndpoint(8001, scheme: "https")
       .Route("well-known", path: "/.well-known/{**catch-all}", target: churrosApi)
       .Route("oauth", path: "/oauth/{**catch-all}", target: churrosApi)
       .Route("api", path: "/api/{**catch-all}", target: churrosApi)
       .Route("login", path: "/login/{**catch-all}", target: churrosApi)
       .Route("share", path: "/share/{**catch-all}", target: churrosApi)
       .Route("swagger", path: "/swagger/{**catch-all}", target: churrosApi)
       // MCP clients POST to exactly "/mcp" with no trailing slash and no sub-path, so a
       // "{**catch-all}"-style route (as used above) would never match it -- that template
       // requires a literal "/" after the prefix even when the catch-all segment is empty.
       // preservePath: true is required here (unlike the routes above): their PathRemovePrefix
       // value is a route-template string that never literally matches a real request path, so
       // it's an accidental no-op; this route's plain "/mcp" literal WOULD match and strip to
       // "/" without preservePath, which would miss ChurrOS.Api's MapMcp("/mcp") endpoint.
       .Route("mcp", path: "/mcp", target: churrosApi, preservePath: true)
       .Route("catch-all", "pwa", new Uri(externalServices["pwa"]!), path: "{**catch-all}", order: 9999);

builder.Build().Run();
