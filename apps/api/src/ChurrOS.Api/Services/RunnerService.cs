using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Deployment.Events;
using ChurrOS.Api.Models.Dtos.Deployment.Remote;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Utils;
using System.Net.Security;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ChurrOS.Api.Services
{
    public class RunnerService
    {
        public class HttpInterceptor : DelegatingHandler
        {
            private readonly byte[] _encryptionKey;
            private readonly int _port;
            private readonly string _environmentName;

            public HttpInterceptor(byte[] encryptionKey, int port, string environmentName)
            {
                _encryptionKey = encryptionKey;
                _port = port;
                _environmentName = environmentName;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                using var hmac = new HMACSHA256(_encryptionKey);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{request.BuildCanonicalUrl()}:{_environmentName}:{timestamp}"))).ToLower();
                request.Headers.Add("X-Environment-Name", _environmentName);
                request.Headers.Add("X-Timestamp", timestamp);
                request.Headers.Add("X-Signature", signature);
                request.Headers.Add("X-Port", _port.ToString());

                // Continue the pipeline
                return await base.SendAsync(request, cancellationToken);
            }
        }

        public class RunnerClient : IDisposable
        {
            private readonly HttpClient _httpClient;
            private readonly string _environmentName;
            private bool disposedValue;

            public HttpClient HttpClient => _httpClient;

            public RunnerClient(HttpClient httpClient, string environmentName)
            {
                _httpClient = httpClient;
                _environmentName = environmentName;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        _httpClient.Dispose();
                    }

                    disposedValue = true;
                }
            }

            public async Task<EnvironmentDefinition> ConnectAsync(CancellationToken cancellationToken)
            {
                var response = await _httpClient.GetAsync("/api/environment", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception($"Cannot connect to environment '{_httpClient.BaseAddress}'. {errorMessage}");
                }
                return JsonSerializer.Deserialize<EnvironmentDefinition>(await response.Content.ReadAsStringAsync(), JsonSettings.Value)!;
            }

            public async Task<DeploymentSummary> DeployAsync(DeploymentRequestItem requestItem, CancellationToken cancellationToken)
            {
                var response = await _httpClient.PostAsJsonAsync("/api/deployments", requestItem, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception(errorMessage);
                }
                return JsonSerializer.Deserialize<DeploymentSummary>(await response.Content.ReadAsStringAsync(), JsonSettings.Value)!;
            }

            public async Task StartAsync(string appName, CancellationToken cancellationToken)
            {
                var response = await _httpClient.PostAsync($"/api/deployments/{appName}/start", new StringContent("{}", new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception(errorMessage);
                }
            }

            public async Task StopAsync(string appName, CancellationToken cancellationToken)
            {
                var response = await _httpClient.PostAsync($"/api/deployments/{appName}/stop", new StringContent("{}", new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception(errorMessage);
                }
            }

            public async Task DeleteAsync(string appName, CancellationToken cancellationToken)
            {
                var response = await _httpClient.DeleteAsync($"/api/deployments/{appName}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return;
                    }
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception(errorMessage);
                }
            }

            public async Task RegisterTemplateAsync(string template, CancellationToken cancellationToken)
            {
                var response = await _httpClient.PostAsync("/api/templates", new StringContent(template, new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain")), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception(errorMessage);
                }
            }

            public async IAsyncEnumerable<GenericEvent> MonitorEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/monitoring/events");
                using var response = await _httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );
                await response.HandleException($"(envName: {_environmentName}, baseAddress: {_httpClient.BaseAddress})");
                await foreach (var @event in SseParser.Create(await response.Content.ReadAsStreamAsync(cancellationToken)).EnumerateAsync(cancellationToken))
                {
                    yield return JsonSerializer.Deserialize<GenericEvent>(@event.Data, JsonSettings.Value)!;
                }
            }

            public async IAsyncEnumerable<string> WatchConsoleAsync(string deploymentName, [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/monitoring/console/{deploymentName}");
                using var response = await _httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );
                await response.HandleException($"(deploymentName: {deploymentName}, envName: {_environmentName}, baseAddress: {_httpClient.BaseAddress})");
                await foreach (var @event in SseParser.Create(await response.Content.ReadAsStreamAsync(cancellationToken)).EnumerateAsync(cancellationToken))
                {
                    yield return JsonSerializer.Deserialize<string>(@event.Data, JsonSettings.Value)!;
                }
            }

            public async IAsyncEnumerable<DeploymentStatus> MonitorStateChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/monitoring/state");
                using var response = await _httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );
                await response.HandleException($"(envName: {_environmentName}, baseAddress: {_httpClient.BaseAddress})");
                await foreach (var @event in SseParser.Create(await response.Content.ReadAsStreamAsync(cancellationToken)).EnumerateAsync(cancellationToken))
                {
                    yield return JsonSerializer.Deserialize<DeploymentStatus>(@event.Data, JsonSettings.Value)!;
                }
            }

            public async Task<DeploymentMetric[]> ScrapeMetricsAsync(CancellationToken cancellationToken)
            {
                var response = await _httpClient.GetAsync("/api/monitoring/metrics", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = await ParseErrorMessage(response);
                    throw new Exception(errorMessage);
                }
                return JsonSerializer.Deserialize<DeploymentMetric[]>(await response.Content.ReadAsStringAsync(), JsonSettings.Value)!;
            }

            private async Task<string> ParseErrorMessage(HttpResponseMessage response)
            {
                string errorMessage = $"{(int)response.StatusCode} {response.StatusCode} (envName: {_environmentName}, baseAddress: {_httpClient.BaseAddress})";
                try
                {
                    var jsonContent = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonSettings.Value);
                    if (jsonContent.TryGetProperty("error", out var jsonError))
                    {
                        errorMessage = jsonError.GetString()!;
                    }
                }
                catch
                {

                }

                return errorMessage;
            }

            public void Dispose()
            {
                // No cambie este código. Coloque el código de limpieza en el método "Dispose(bool disposing)".
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public RunnerClient CreateClient(string host, string name, int port, string encryptionKey, TimeSpan? timeout = null)
        {
            return new RunnerClient(CreateHttpClient(host, name, port, encryptionKey, timeout), name);
        }

        public HttpClient CreateHttpClient(string host, string name, int port, string encryptionKey, TimeSpan? timeout = null)
        {
            var interceptor = new HttpInterceptor(Convert.FromBase64String(encryptionKey), port, name)
            {
                InnerHandler = new HttpClientHandler()
            };

            var client = new HttpClient(interceptor);
            client.BaseAddress = new Uri(host);
            if (timeout != null)
            {
                client.Timeout = timeout.Value;
            }

            return client;
        }

        public static bool ValidateWithPrivateCa(X509Certificate2 serverCert, X509Certificate2 privateRootCa)
        {
            using var customChain = new X509Chain();

            customChain.ChainPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag
            };

            customChain.ChainPolicy.CustomTrustStore.Add(privateRootCa);

            var result = customChain.Build(serverCert);
            return result;
        }

        public static bool ValidateWithOsTrust(X509Certificate2 cert, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // If .NET already says it's valid, accept it
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // Otherwise, explicitly rebuild chain using OS trust
            using var osChain = new X509Chain();
            osChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            osChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            osChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            return osChain.Build(cert);
        }
    }
}
