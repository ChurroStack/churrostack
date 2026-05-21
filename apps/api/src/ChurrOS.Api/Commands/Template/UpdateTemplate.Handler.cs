using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Template
{
    public class UpdateTemplateHandler : IRequestHandler<UpdateTemplate, ValueTask<TemplateItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly TemplateService _templateService;
        private readonly IIdGeneratorService _idGeneratorService;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public UpdateTemplateHandler(ChurrosDbContext context, ITenantResolver tenantResolver, TemplateService templateService, IIdGeneratorService idGeneratorService, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _templateService = templateService;
            _idGeneratorService = idGeneratorService;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<TemplateItem> Handle(UpdateTemplate request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            ArgumentException.ThrowIfNullOrWhiteSpace(request.Name, nameof(request.Name));
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Target, nameof(request.Target));

            var template = _context.Set<Domain.Template>()
                .FirstOrDefault(o => o.Name == request.Name && o.Target == request.Target);

            if (template == null)
            {
                throw new NotFoundException($"Tempate with name '{request.Name}' and target '{request.Target}' was not found.");
            }

            var rawTemplate = request.Content.Trim();
            var hash = rawTemplate.GetSha1Hash();
            var templateJson = await _templateService.EvaluateAsync(rawTemplate);
            var templateDef = templateJson.Deserialize<TemplateDefinition>(JsonSettings.Value)!;
            var now = DateTimeOffset.UtcNow;

            var category = _context.Set<TemplateCategory>().FirstOrDefault(o => o.Name == templateDef.Category!.Name);

            if (category is null)
            {
                _context.Set<TemplateCategory>().Add(category = new TemplateCategory(_tenantResolver.AccountId, _idGeneratorService.CreateLongId(), templateDef.Category!.Name, templateDef.Category.Title, templateDef.Category.Icon, templateDef.Category.Translation));
            }

            template.Content = rawTemplate;
            template.Definition = templateDef;
            template.Hash = hash;
            template.CategoryId = category.Id;
            template.ModifiedAt = DateTimeOffset.Now;
            template.ModifiedById = _context.IdentityId;

            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<TemplateItem>(template);
        }
    }
}
