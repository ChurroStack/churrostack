using ChurrOS.Api.Data;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Template
{
    public class GetTemplateIdByNameHandler : IRequestHandler<GetTemplateIdByName, ValueTask<long>>
    {
        private readonly ChurrosDbContext _context;

        public GetTemplateIdByNameHandler(ChurrosDbContext context)
        {
            _context = context;
        }

        public async ValueTask<long> Handle(GetTemplateIdByName request, CancellationToken cancellationToken)
        {
            var query = _context.Set<Domain.Template>()
                .AsNoTracking()
                .Where(o => o.Name == request.Name)
                .Select(o => (long?)o.Id);

            var id = await query.FirstOrDefaultAsync(cancellationToken);

            if (!id.HasValue)
                throw new NotFoundException($"Template with name '{request.Name}' was not found.");

            return id.Value;
        }
    }
}
