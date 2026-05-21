using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationIdByNameHandler : IRequestHandler<GetApplicationIdByName, ValueTask<ApplicationIdentifier>>
    {
        private readonly ChurrosDbContext _context;

        public GetApplicationIdByNameHandler(ChurrosDbContext context)
        {
            _context = context;
        }

        public async ValueTask<ApplicationIdentifier> Handle(GetApplicationIdByName request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();
            var item = await repo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.AclId, EnvironmentAclId = o.Environment!.AclId })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            return new ApplicationIdentifier(item.Id, item.AclId, item.EnvironmentAclId);
        }
    }
}
