using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmDestinationModelsHandler : IRequestHandler<GetLlmDestinationModels, ValueTask<OaiModels>>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly RunnerService _runnerService;

        public GetLlmDestinationModelsHandler(IHttpClientFactory httpClientFactory, ChurrosDbContext context, IMediator mediator, RunnerService runnerService)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _mediator = mediator;
            _runnerService = runnerService;
        }

        public async ValueTask<OaiModels> Handle(GetLlmDestinationModels request, CancellationToken cancellationToken)
        {
            HttpClient httpClient;

            var url = new Uri(request.Body.Uri);
            if (url.Host.Contains("azure.com"))
            {
                throw new ArgumentException("Microsoft Azure OpenAI is not supported for model listing.");
            }
            else
            {
                if (url.Scheme == "internal")
                {
                    var query = (IQueryable<Domain.Application>)_context.Set<Domain.Application>()
                        .AsNoTracking()
                        .Include(x => x.Environment);
                    if (long.TryParse(url.Host, out var hostAppId))
                    {
                        query = query.Where(x => x.Id == hostAppId);
                    }
                    else
                    {
                        query = query.Where(x => x.Name == url.Host);
                    }
                    var app = await query
                        .Select(x => new { x.Name, EnvironmentName = x.Environment.Name, EnvironmentId = x.Environment.Id, EnvironmentHost = x.Environment.Host, EnvironmentPort = x.Environment.Port, EnvironmentEncryptionKey = x.Environment.EncryptionKey, x.Ports })
                        .FirstOrDefaultAsync();

                    if (app == null)
                        throw new ArgumentException($"Invalid application '{url.Host}'");
                    var port = app.Ports?.FirstOrDefault(o => o.Protocol == Models.Dtos.Template.Definition.ProtocolType.OpenAI);
                    if (port is null)
                    {
                        throw new ArgumentException($"The application doesnt publish an OpenAI compatible port");
                    }

                    var ecParts = app.EnvironmentEncryptionKey.Split(':');
                    var encryptionKey = AesGcmEncryption.Decrypt(ecParts[0], _context.AccountEncryptionKey, ecParts[1]);
                    httpClient = _runnerService.CreateHttpClient(app.EnvironmentHost[1], app.EnvironmentName, app.EnvironmentPort, encryptionKey);

                    var parts = new List<string>
                    {
                        app.EnvironmentHost[1].TrimEnd('/'),
                        $"share/{app.Name}/{port.Name}"
                    };
                    var destinationUriBuilder = new UriBuilder(request.Body.Uri);
                    if (!string.IsNullOrEmpty(destinationUriBuilder.Path) && destinationUriBuilder.Path != "/")
                    {
                        parts.Add(destinationUriBuilder.Path.Trim('/'));
                    }
                    parts.Add("models");
                    url = new Uri(string.Join('/', parts));
                }
                else
                {
                    url = new Uri($"{url.Scheme}://{url.Host}:{url.Port}{url.AbsolutePath}/models{url.Query}");
                    httpClient = _httpClientFactory.CreateClient();
                }
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(request.Body.ApiKey))
            {
                message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.Body.ApiKey);
            }

            using var response = await httpClient.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();
            var oaiModels = await response.Content.ReadFromJsonAsync<OaiModels>(cancellationToken: cancellationToken);
            return oaiModels!;
        }
    }
}
