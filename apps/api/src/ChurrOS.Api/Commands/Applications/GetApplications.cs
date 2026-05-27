using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplications : IRequest<GetApplications, ValueTask<QueryResult<ApplicationSummary>>>
    {
        public class ApplicationQueryRequest : QueryRequest
        {
            public ApplicationMode? Mode { get; set; }

            public string? Environment { get; set; }

            public string? CreatedBy { get; set; }

            public string[]? Tags { get; set; }

            public ApplicationQueryRequest() : base() { }

            public ApplicationQueryRequest(string environment, int? page = DefaultPage, int? pageSize = DefaultPageSize, string? search = null, ApplicationMode? mode = null, string? createdBy = null, string[]? tags = null) : base(page, pageSize, search)
            {
                Environment = environment;
                Mode = mode;
                CreatedBy = createdBy;
                Tags = tags;
            }
        }

        public ApplicationQueryRequest Query { get; private set; }

        public GetApplications(ApplicationQueryRequest query)
        {
            Query = query;
        }
    }
}
