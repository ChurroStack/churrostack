using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class TestLlmDestinationHandler : IRequestHandler<TestLlmDestination, Task>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly RunnerService _runnerService;

        public TestLlmDestinationHandler(IHttpClientFactory httpClientFactory, ChurrosDbContext context, IMediator mediator, RunnerService runnerService)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _mediator = mediator;
            _runnerService = runnerService;
        }

        public async Task Handle(TestLlmDestination request, CancellationToken cancellationToken)
        {
            var llm = await _context.Set<Domain.Llm>()
                .AsNoTracking()
                .Where(o => o.Id == request.LlmId)
                .Select(o => new { o.AclId })
                .FirstOrDefaultAsync(cancellationToken);

            if (llm == null)
                throw new NotFoundException($"Llm with id '{request.LlmId}' was not found.");

            if (!await _mediator.Send(new IsAdminOrHasAcl(llm.AclId, Permission.Write), cancellationToken))
                throw new UnauthorizedAccessException("You do not have permission to test this LLM destination.");

            HttpClient httpClient;
            var url = new Uri(request.Body.Uri);
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
                parts.Add("chat/completions");
                url = new Uri(string.Join('/', parts));
            }
            else
            {
                url = new Uri($"{url.Scheme}://{url.Host}:{url.Port}{url.AbsolutePath}/chat/completions{url.Query}");
                httpClient = _httpClientFactory.CreateClient();
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, url);
            message.Content = new StringContent($$"""
            {
              "model": "{{request.Body.Model}}",
              "messages": [
                { "role": "user", "content": "Hello!" }
              ]
            }
            """, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(request.Body.ApiKey))
            {
                message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.Body.ApiKey);
            }

            using var response = await httpClient.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}
