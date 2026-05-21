using ChurrunKubernetes.Data;
using ChurrunKubernetes.Middlewares;
using ChurrunKubernetes.Models.Logs;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Services.Share;
using ChurrunKubernetes.Services.State;
using ChurrunKubernetes.Utils.AspNet;
using DispatchR.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Transforms;

namespace ChurrunKubernetes
{
    public class Program
    {
        public static async Task Main(string[] args)
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
            builder.Services
                .AddControllers(options =>
                {
                    options.Filters.Add(new ResponseExceptionFilter());
                    options.InputFormatters.Add(new TextInputFormatter());
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ApplyDefaultOptions();
                    JsonSettings.Value = options.JsonSerializerOptions;
                });
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            var databaseConnectionString = builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Connection string 'Database' not found.");
            builder.Services.AddDbContext<ChurrunDbContext>(options =>
            {
                options.UseSqlite(databaseConnectionString.ToString(), o =>
                {
                    o.MigrationsAssembly(typeof(ChurrunDbContext).Assembly.FullName);
                    o.CommandTimeout((int)TimeSpan.FromMinutes(1).TotalSeconds);
                });
#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif
            });

            builder.Services
                .AddAuthentication("HmacScheme")
                .AddScheme<AuthenticationSchemeOptions, HmacAuthenticationHandler>("HmacScheme", options => { });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("HmacScheme", policy =>
                {
                    policy.AddAuthenticationSchemes("HmacScheme");
                    policy.RequireAuthenticatedUser();
                });
            });

            builder.Services.AddMapster();
            builder.Services.AddDispatchR(cfg => cfg.Assemblies.Add(typeof(Program).Assembly));
            builder.Services.AddSingleton<TemplateService>();
            builder.Services.AddSingleton<KubernetesService>();
            builder.Services.AddLazyCache();
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<IdGenerationService, IdGenerationService>();
            builder.Services.AddSingleton<EventsStateService<KubernetesGenericEvent>, EventsStateService<KubernetesGenericEvent>>();
            builder.Services.AddSingleton<EventsStateService<KubernetesDeploymentEvent>, EventsStateService<KubernetesDeploymentEvent>>();
            builder.Services.AddHostedService<Jobs.KubernetesEventsMonitoringJob>();
            builder.Services.AddHostedService<Jobs.KubernetesDeploymentsMonitoringJob>();
            builder.Services.AddSingleton<ProxyConfigurationProvider>();
            builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<ProxyConfigurationProvider>());
            builder.Services.AddSingleton<ILoadBalancingPolicy, DestinationSelectionPolicy>();
            builder.Services.AddReverseProxy().AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(transformContext =>
                {
                    transformContext.ProxyRequest.Headers.Remove("Authorization");
                    transformContext.ProxyRequest.Headers.Remove("X-Environment-Name");
                    transformContext.ProxyRequest.Headers.Remove("X-Port");
                    transformContext.ProxyRequest.Headers.Remove("X-Signature");
                    transformContext.ProxyRequest.Headers.Remove("X-Original-Host");
                    transformContext.ProxyRequest.Headers.Remove("X-Original-For");
                    transformContext.ProxyRequest.Headers.Remove("X-Timestamp");
                    return ValueTask.CompletedTask;
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "v1");
                });
            }

            app.UseForwardedHeaders();
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
            });

            app.UseWebSockets();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.MapReverseProxy();

            // Database migration & seeding
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ChurrunDbContext>();
            context.Database.Migrate();

            var proxyConfig = scope.ServiceProvider.GetRequiredService<ProxyConfigurationProvider>();
            await proxyConfig.Initialize();

            app.Run();
        }
    }
}
