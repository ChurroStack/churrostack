using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class CreateApiKeyHandler : IRequestHandler<CreateApiKey, ValueTask<CreateApiKey.CreateApiKeyResponse>>
    {
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IIdGeneratorService _idGenerationService;
        private readonly IMediator _mediator;

        public CreateApiKeyHandler(ChurrosDbContext context, ITenantResolver tenantResolver, IIdGeneratorService idGenerationService, IMediator mediator)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _idGenerationService = idGenerationService;
            _mediator = mediator;
        }

        public async ValueTask<CreateApiKey.CreateApiKeyResponse> Handle(CreateApiKey request, CancellationToken cancellationToken)
        {
            var identityId = _context.IdentityId;
            if (!string.IsNullOrWhiteSpace(request.Body.IdentityName))
            {
                var identity = await _context
                    .Set<Domain.Identity>()
                    .Where(o => o.Name == request.Body.IdentityName)
                    .FirstOrDefaultAsync(cancellationToken);
                if (identity is null)
                {
                    throw new NotFoundException($"Identity '{request.Body.IdentityName}' does not exists or you do not have access.");
                }

                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Manage), cancellationToken);

                if (identity.Id != _context.IdentityId && identity.CreatedById != _context.IdentityId && (!identity.AclId.HasValue || !identityAcls.ContainsKey(identity.AclId.Value)))
                {
                    var isAdministrator = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
                    if (!isAdministrator)
                    {
                        throw new UnauthorizedAccessException($"You do not have permission to create API keys for the entity '{request.Body.IdentityName}'.");
                    }
                }
                identityId = identity.Id;
            }

            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            var apiKey = Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            var id = _idGenerationService.CreateLongId();
            var now = DateTimeOffset.UtcNow;
            var hash = apiKey.GetSha256Hash();
            var expiresAt = request.Body.ExpiresAt ?? DateTime.Now.AddDays(90);
            _context.Set<Domain.ApiKey>().Add(new Domain.ApiKey(_tenantResolver.AccountId, id, request.Body.Description ?? "", hash, expiresAt, identityId, now, _context.IdentityId, now, _context.IdentityId));
            await _context.SaveChangesAsync(cancellationToken);

            return new CreateApiKey.CreateApiKeyResponse(id.ToString(), apiKey, expiresAt);
        }
    }
}
