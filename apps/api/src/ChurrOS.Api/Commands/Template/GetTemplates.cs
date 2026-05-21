using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Template;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Template
{
    public class GetTemplates : IRequest<GetTemplates, ValueTask<QueryResult<TemplateSummary>>>
    {
        public class TemplateQueryRequest : QueryRequest
        {
            public TemplateType? Type { get; set; }

            public TemplateQueryRequest() : base() { }

            public TemplateQueryRequest(int? page = DefaultPage, int? pageSize = DefaultPageSize, string? search = null, TemplateType? type = null) : base(page, pageSize, search)
            {
                Type = type;
            }
        }

        public TemplateQueryRequest Query { get; private set; }

        public GetTemplates(TemplateQueryRequest query)
        {
            Query = query;
        }
    }
}
