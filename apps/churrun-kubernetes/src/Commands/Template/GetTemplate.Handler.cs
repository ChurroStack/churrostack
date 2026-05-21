using ChurrunKubernetes.Data;
using ChurrunKubernetes.Models.Dtos.Exceptions;
using ChurrunKubernetes.Models.Dtos.Template;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrunKubernetes.Commands.Template
{
    public class GetTemplateHandler : IRequestHandler<GetTemplate, ValueTask<TemplateSummary>>
    {
        private readonly ChurrunDbContext _context;
        private readonly IMapper _mapper;

        public GetTemplateHandler(ChurrunDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async ValueTask<TemplateSummary> Handle(GetTemplate request, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(request.Name, nameof(request.Name));

            var nameParts = request.Name.Split('/', 2);

            var query = _context.Set<Domain.Template>()
                .AsNoTracking();
            if (nameParts.Length == 2)
            {
                var hash = Convert.FromBase64String(nameParts[1]);
                query = query.Where(o => o.Name == nameParts[0] && o.Hash == hash);
            }
            else
            {
                query = query.Where(o => o.Name == nameParts[0]);
            }

            var item = await query.OrderByDescending(o => o.CreatedOn)
                .FirstOrDefaultAsync(cancellationToken);

            if (item == null)
                throw new HttpException(404, $"Template '{request.Name}' not found. Please register your template first");

            return _mapper.Map<TemplateSummary>(item);
        }
    }
}
