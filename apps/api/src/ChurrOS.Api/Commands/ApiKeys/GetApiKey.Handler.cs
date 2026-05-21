using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.ApiKey;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class GetApiKeyHandler : IRequestHandler<GetApiKey, ValueTask<ApiKeyItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;

        public GetApiKeyHandler(ChurrosDbContext context, IMediator mediator, IMapper mapper)
        {
            _context = context;
            _mediator = mediator;
            _mapper = mapper;
        }

        public async ValueTask<ApiKeyItem> Handle(GetApiKey request, CancellationToken cancellationToken)
        {
            var apiKey = await _context.Set<Domain.ApiKey>()
                .Include(o => o.Identity)
                .Where(o => o.Id == request.Id)
                .FirstOrDefaultAsync();

            if (apiKey is null)
            {
                throw new NotFoundException($"API Key with id '{request.Id}' was not found.");
            }

            var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Manage), cancellationToken);
            if (apiKey.IdentityId != _context.IdentityId && apiKey.Identity!.CreatedById != _context.IdentityId && (!apiKey.Identity!.AclId.HasValue || !identityAcls.ContainsKey(apiKey.Identity!.AclId.Value)))
            {
                var isAdministrator = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
                if (!isAdministrator)
                {
                    throw new UnauthorizedAccessException("You do not have permission to delete this API key.");
                }
            }

            return _mapper.Map<ApiKeyItem>(apiKey!);
        }
    }
}
