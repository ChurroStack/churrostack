using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Template
{
    public class CreateTemplateHandler : IRequestHandler<CreateTemplate, ValueTask<TemplateItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly TemplateService _templateService;
        private readonly IIdGeneratorService _idGeneratorService;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public CreateTemplateHandler(ChurrosDbContext context, ITenantResolver tenantResolver, TemplateService templateService, IIdGeneratorService idGeneratorService, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _templateService = templateService;
            _idGeneratorService = idGeneratorService;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<TemplateItem> Handle(CreateTemplate request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            ArgumentException.ThrowIfNullOrWhiteSpace(request.Content, nameof(request.Content));

            var rawTemplate = request.Content.Trim();
            var hash = rawTemplate.GetSha1Hash();
            var templateJson = await _templateService.EvaluateAsync(rawTemplate);
            var templateDef = templateJson.Deserialize<TemplateDefinition>(JsonSettings.Value)!;
            var now = DateTimeOffset.UtcNow;
            var metadata = JsonElement.Parse("{}");

            var category = _context.Set<TemplateCategory>().FirstOrDefault(o => o.Name == templateDef.Category!.Name);

            if (category is null)
            {
                _context.Set<TemplateCategory>().Add(category = new TemplateCategory(_tenantResolver.AccountId, _idGeneratorService.CreateLongId(), templateDef.Category!.Name, templateDef.Category.Title, templateDef.Category.Icon, templateDef.Category.Translation));
            }

            var template = new Domain.Template(_tenantResolver.AccountId, _idGeneratorService.CreateLongId(), category.Id, hash, templateDef, rawTemplate, metadata, now, _context.IdentityId, now, _context.IdentityId);
            _context.Set<Domain.Template>().Add(template);

            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<TemplateItem>(template);
        }
    }
}
