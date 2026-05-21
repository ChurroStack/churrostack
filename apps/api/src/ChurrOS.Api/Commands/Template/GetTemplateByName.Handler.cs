using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Template;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Template
{
    public class GetTemplateByNameHandler : IRequestHandler<GetTemplateByName, ValueTask<TemplateItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;

        public GetTemplateByNameHandler(ChurrosDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async ValueTask<TemplateItem> Handle(GetTemplateByName request, CancellationToken cancellationToken)
        {
            var query = (IQueryable<Domain.Template>)_context.Set<Domain.Template>()
                .AsNoTracking()
                .Include(o => o.Category)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy);

            if (long.TryParse(request.Name, out var templateId))
            {
                query = query.Where(c => c.Id == templateId);
            }
            else
            {
                query = query.Where(c => c.Name == request.Name);
            }

            var item = await query.FirstOrDefaultAsync(cancellationToken);

            if (item is null)
                throw new NotFoundException($"Template with name '{request.Name}' was not found.");

            return _mapper.Map<TemplateItem>(item);
        }
    }
}
