using ChurrunKubernetes.Models.Dtos;
using ChurrunKubernetes.Models.Dtos.Template;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Template
{
    public class GetTemplates : IRequest<GetTemplates, ValueTask<QueryResult<TemplateSummary>>>
    {
        public QueryRequest Query { get; private set; }

        public GetTemplates(QueryRequest query)
        {
            Query = query;
        }
    }
}
