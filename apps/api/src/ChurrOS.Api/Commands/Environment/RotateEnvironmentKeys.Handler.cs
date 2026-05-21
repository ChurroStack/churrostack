using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Environment
{
    public class RotateEnvironmentKeysHandler : IRequestHandler<RotateEnvironmentKeys, ValueTask<EnvironmentKeysItem>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _dbContext;
        private readonly TemplateService _templateService;

        public RotateEnvironmentKeysHandler(IMediator mediator, ChurrosDbContext dbContext, TemplateService templateService)
        {
            _mediator = mediator;
            _dbContext = dbContext;
            _templateService = templateService;
        }

        public async ValueTask<EnvironmentKeysItem> Handle(RotateEnvironmentKeys request, CancellationToken cancellationToken)
        {
            var envItem = await _mediator.Send(new GetEnvironmentByName(request.Name), cancellationToken);

            var env = await _dbContext.Set<Domain.Environment>()
                .Where(o => o.Name == request.Name)
                .SingleAsync(cancellationToken: cancellationToken);

            if (!await _mediator.Send(new IsAdminOrHasAcl(env.AclId, Permission.Manage), cancellationToken))
                throw new UnauthorizedAccessException();

            var keys = SshKeyGenerator.GenerateSshKeyPair();

            var accountEncryptionKey = _dbContext.AccountEncryptionKey;
            var encryptionKey = Convert.ToBase64String(AesGcmEncryption.GenerateKey());

            var iv = AesGcmEncryption.GenerateBase64Iv();
            var protectedEncryptionKey = AesGcmEncryption.Encrypt(encryptionKey, accountEncryptionKey, iv);

            env.EncryptionKey = $"{protectedEncryptionKey}:{iv}";
            env.SshPublicKey = keys.PublicKey;
            var pubKey = keys.PublicKey.AsSshPubKey($"{envItem.Name}@{env.Host[0]}");

            await _dbContext.SaveChangesAsync();

            string host = env.Host[0];
            int port = 6443;
            var hostParts = host.Split(':');
            if (hostParts.Length == 2 && int.TryParse(hostParts[1], out int parsedPort))
            {
                port = parsedPort;
                host = hostParts[0];
            }

            string hostFingerprint = "";
            string knowHosts = "";

            try
            {
                hostFingerprint = await GetHostFingerprint(host, keys.PemPrivateKey, port: port);
                if (IPAddress.IsValid(host))
                {
                    knowHosts = $"{host} {hostFingerprint}";
                }
                else
                {
                    var ipAddress = await Dns.GetHostAddressesAsync(host);
                    knowHosts = $"{host},{string.Join(',', ipAddress)} {hostFingerprint}";
                }
            }
            catch
            {
#if !DEBUG
                throw;
#endif
                // IGNORE in DEBUG
            }

            var yamlValues = Assembly.GetExecutingAssembly().ReadResourceAsString("Resources.helm-values.yaml");
            yamlValues = await _templateService.TransformAsync(yamlValues, JsonSerializer.SerializeToElement(
                new
                {
                    name = env.Name,
                    encryption_key = encryptionKey,
                    tunnel_host = hostParts[0],
                    tunnel_port = env.Port,
                    tunnel_private_key = keys.PemPrivateKey,
                    tunnel_public_key = hostFingerprint,
                })
            );
            var @namespace = $"churrun-{env.Name}";
            return new EnvironmentKeysItem(pubKey, keys.PemPrivateKey, encryptionKey, env.Host[0], env.Port, knowHosts, @namespace, yamlValues);
        }

        private async Task<string> GetHostFingerprint(string host, string privateKey, int port = 8443, string user = "tunnel")
        {
            var ms = new MemoryStream();
            var bytes = Encoding.UTF8.GetBytes(privateKey);
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);

            var tcs = new CancellationTokenSource();
            string fingerprint = string.Empty;
            using (var client = new SshClient(host, port, user, new PrivateKeyFile(ms)))
            {
                client.HostKeyReceived += (object? sender, Renci.SshNet.Common.HostKeyEventArgs e) =>
                {
                    string keyType = e.HostKeyName; // e.g. "ssh-ed25519"
                    string base64Key = Convert.ToBase64String(e.HostKey);
                    fingerprint = $"{keyType} {base64Key}";
                    tcs.Cancel();
                };

                client.Connect();

                while (!tcs.IsCancellationRequested)
                {
                    await Task.Delay(100);
                }
            }

            return fingerprint;
        }
    }
}
