using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Llm
{
    public class UpdateLlmHandler : IRequestHandler<UpdateLlm, ValueTask<LlmItem>>
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;

        public UpdateLlmHandler(
            IMediator mediator,
            IMapper mapper,
            ChurrosDbContext context,
            ITenantResolver tenantResolver,
            ICacheService cacheService)
        {
            _mediator = mediator;
            _mapper = mapper;
            _context = context;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
        }

        public async ValueTask<LlmItem> Handle(UpdateLlm request, CancellationToken cancellationToken)
        {
            var llm = await _context.Set<Domain.Llm>()
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Id == request.LlmId);

            if (llm == null)
            {
                throw new NotFoundException($"Llm with id '{request.LlmId}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(llm.AclId, Permission.Write), cancellationToken))
            {
                throw new UnauthorizedAccessException("You do not have permission to update this LLM.");
            }

            var membersToPurge = await _context.Set<Domain.AclMember>()
                .Include(o => o.Identity)
                .Where(o => o.AclId == llm.AclId)
                .Select(o => o.Identity!.Id)
                .ToListAsync(cancellationToken);

            long[]? updatedMembers = null;
            foreach (var entry in request.Body.EnumerateObject())
            {
                switch (entry.Name)
                {
                    case "names":
                        llm.Names = entry.Value.Deserialize<string[]>(JsonSettings.Value)!;
                        if (llm.Names == null || llm.Names.Length == 0 || llm.Names.Any(string.IsNullOrWhiteSpace))
                        {
                            throw new ArgumentException("Llm must have at least one name.");
                        }
                        break;
                    case "destination":
                        llm.Destination = entry.Value.Deserialize<LLmDestinationItem[]>(JsonSettings.Value)!;
                        break;
                    case "fallback":
                        llm.Fallback = entry.Value.Deserialize<LLmDestinationItem>(JsonSettings.Value)!;
                        break;
                    case "members":
                        {
                            if (!await _mediator.Send(new IsAdminOrHasAcl(llm.AclId, Permission.Manage), cancellationToken))
                                throw new UnauthorizedAccessException("You do not have permission to manage this LLM security members.");
                            updatedMembers = await _mediator.UpdateAclAsync(membersToPurge, _context, _tenantResolver.AccountId, llm.AclId, entry.Value.Deserialize<MemberItem[]>(JsonSettings.Value)!, cancellationToken);
                            break;
                        }
                    default:
                        throw new ArgumentException($"Cannot update member '{entry.Name}' for this LLM.");
                }
            }

            llm.ModifiedAt = DateTimeOffset.Now;
            llm.ModifiedById = _context.IdentityId;

            await _context.SaveChangesAsync();

            foreach (var identityId in Array.Empty<long>().Union(membersToPurge ?? []).Union(updatedMembers ?? []).Distinct())
            {
                await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityId}");
            }

            return _mapper.Map<Domain.Llm, LlmItem>(llm);
        }
    }
}
