using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class DeleteLlmHandler : IRequestHandler<DeleteLlm, Task>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;

        public DeleteLlmHandler(IMediator mediator, ChurrosDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        public async Task Handle(DeleteLlm request, CancellationToken cancellationToken)
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

            _context.Set<Domain.Llm>().Remove(llm);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
