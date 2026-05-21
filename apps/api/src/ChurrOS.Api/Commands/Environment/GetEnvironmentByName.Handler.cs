using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentByNameHandler : IRequestHandler<GetEnvironmentByName, ValueTask<EnvironmentItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetEnvironmentByNameHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<EnvironmentItem> Handle(GetEnvironmentByName request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Environment>();
            var item = await repo
                .AsNoTracking()
                .Include(o => o.Account)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (item == null)
            {
                throw new NotFoundException($"Environment with name '{request.Name}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(item.AclId, Permission.Read), cancellationToken))
                throw new UnauthorizedAccessException();

            var result = _mapper.Map<Domain.Environment, EnvironmentItem>(item!);
            var acl = await _context
                .Set<Domain.AclMember>()
                .AsNoTracking()
                .Include(o => o.Identity)
                .Where(o => o.AclId == item.AclId)
                .ToListAsync();
            result.Members = acl.Select(o => _mapper.Map<Domain.AclMember, MemberSummary>(o)).ToArray();
            return result;
        }
    }
}
