using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Template
{
    public class DeleteTemplateHandler : IRequestHandler<DeleteTemplate, Task>
    {
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;

        public DeleteTemplateHandler(ChurrosDbContext context, ITenantResolver tenantResolver, IMediator mediator)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
        }

        public async Task Handle(DeleteTemplate request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            // TODO: Deallocate everything related to this template

            await _context.Set<Domain.Template>().Where(o => o.Name == request.Name).ExecuteDeleteAsync();
        }
    }
}
