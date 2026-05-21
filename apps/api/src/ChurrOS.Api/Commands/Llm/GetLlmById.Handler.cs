using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmByIdHandler : IRequestHandler<GetLlmById, ValueTask<LlmItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetLlmByIdHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<LlmItem> Handle(GetLlmById request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Llm>();
            var item = await repo
                .AsNoTracking()
                .Include(o => o.Account)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .Include("Acl.Members.Identity")
                .FirstOrDefaultAsync(o => o.Id == request.LlmId);

            if (item == null)
            {
                throw new NotFoundException($"LLm with id '{request.LlmId}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(item.AclId, Permission.Read), cancellationToken))
                throw new UnauthorizedAccessException();

            return _mapper.Map<Domain.Llm, LlmItem>(item!);
        }
    }
}
