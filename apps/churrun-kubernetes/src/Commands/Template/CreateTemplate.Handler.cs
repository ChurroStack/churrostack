using ChurrunKubernetes.Data;
using ChurrunKubernetes.Models.Dtos.Template;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Utils;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrunKubernetes.Commands.Template
{
    public class CreateTemplateHandler : IRequestHandler<CreateTemplate, ValueTask<TemplateSummary>>
    {
        private readonly ChurrunDbContext _context;
        private readonly IMapper _mapper;
        private readonly IdGenerationService _idGenerationService;
        private readonly TemplateService _templateService;

        public CreateTemplateHandler(ChurrunDbContext context, IMapper mapper, IdGenerationService idGenerationService, TemplateService templateService)
        {
            _context = context;
            _mapper = mapper;
            _idGenerationService = idGenerationService;
            _templateService = templateService;
        }

        public async ValueTask<TemplateSummary> Handle(CreateTemplate request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Template>();
            var id = _idGenerationService.CreateLongId();

            var content = request.Template?.Trim();
            ArgumentException.ThrowIfNullOrWhiteSpace(content, nameof(request.Template));
            var templateJson = await _templateService.EvaluateAsync(content);
            var hash = content.GetSha1Hash();
            var name = templateJson.GetProperty("name").GetString()!;

            Domain.Template? template = await repo.FirstOrDefaultAsync(o => o.Name == name && o.Hash == hash);
            if (template is not null)
            {
                return _mapper.Map<TemplateSummary>(template);
            }

            if (!NamingUtils.IsValidTemplateName(name))
            {
                throw new ArgumentException("The provided name is not valid. Only lowercase alphanumeric and hypens (-) are allowed.", nameof(name));
            }
            if (templateJson.TryGetProperty("extensions", out var jsonExtensions) && jsonExtensions.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var extension in jsonExtensions.EnumerateArray())
                {
                    var extensionName = extension.GetProperty("name").GetString()!;
                    if (!NamingUtils.IsValidName(extensionName))
                    {
                        throw new ArgumentException("The provided extension name is not valid. Only lowercase alphanumeric and hypens (-) are allowed.", nameof(extensionName));
                    }
                }
            }
            if (templateJson.TryGetProperty("ports", out var jsonPorts) && jsonPorts.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var port in jsonPorts.EnumerateArray())
                {
                    var portName = port.GetProperty("name").GetString()!;
                    if (!NamingUtils.IsValidName(portName))
                    {
                        throw new ArgumentException("The provided port name is not valid. Only lowercase alphanumeric and hypens (-) are allowed.", nameof(portName));
                    }
                }
            }

            template = new Domain.Template(id, name, hash, content, DateTime.Now);
            repo.Add(template);

            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<TemplateSummary>(template);
        }
    }
}
