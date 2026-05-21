using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace ChurrOS.TunnelService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxRequestBodySize = 10_000_000_000;
            });

            builder.Services.Configure<FormOptions>(o =>
            {
                o.MultipartBodyLengthLimit = 10_000_000_000;
            });

            // Add services to the container.

            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy());

            builder.Services
                .AddReverseProxy()
                .LoadFromMemory(
                    [
                        new RouteConfig
                        {
                            RouteId = "dynamic",
                            ClusterId = "dynamic",
                            Match = new RouteMatch { Path = "{**catch-all}" },
                            MaxRequestBodySize = -1
                        }
                    ],
                    [
                        new ClusterConfig
                        {
                            ClusterId = "dynamic",
                            Destinations = new Dictionary<string, DestinationConfig>
                            {
                                { "default", new DestinationConfig { Address = "http://localhost:0/" } }
                            }
                        }
                    ])
                .AddTransforms(builderContext =>
                {
                    builderContext.AddRequestTransform(async transformContext =>
                    {
                        if (transformContext.HttpContext.Request.Headers.TryGetValue("X-Port", out var port))
                        {
                            if (!transformContext.HttpContext.Request.Headers.TryGetValue("X-Schema", out var schema))
                            {
                                schema = "http";
                            }
                            if (string.IsNullOrWhiteSpace(schema) || (!"http".Equals(schema, StringComparison.InvariantCultureIgnoreCase) && !"https".Equals(schema, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                transformContext.HttpContext.Response.StatusCode = 400;
                                await transformContext.HttpContext.Response.WriteAsync("Invalid X-Schema.");
                                throw new ArgumentException("Invalid X-Schema.");

                            }
                            var dest = $"{schema}://localhost:{port}";
                            transformContext.ProxyRequest.RequestUri = new Uri(dest + transformContext.HttpContext.Request.Path + transformContext.HttpContext.Request.QueryString);
                        }
                        else
                        {
                            transformContext.HttpContext.Response.StatusCode = 400;
                            await transformContext.HttpContext.Response.WriteAsync("X-Port header not found.");
                            throw new ArgumentException("X-Port header not found.");
                        }
                    });
                });

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.MapHealthChecks("/health");

            app.MapReverseProxy();

            app.Run();
        }
    }
}
