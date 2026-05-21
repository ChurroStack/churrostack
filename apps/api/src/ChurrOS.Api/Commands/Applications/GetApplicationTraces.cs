using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationTraces : IRequest<GetApplicationTraces, ValueTask<QueryResult<ApplicationTraceItem>>>
    {
        public class TracesQueryRequest : QueryRequest
        {
            public DateTimeOffset? From { get; set; }
            public DateTimeOffset? To { get; set; }
            public string? IdentityName { get; set; }

            public TracesQueryRequest()
            {
            }

            public TracesQueryRequest(DateTimeOffset? from, DateTimeOffset? to, int? page = 1, int? pageSize = 25, string? search = null) : base(page, pageSize, search)
            {
                From = from;
                To = to;
            }
        }

        public string AppName { get; private set; }

        public TracesQueryRequest Query { get; private set; }

        public GetApplicationTraces(string appName, TracesQueryRequest query)
        {
            AppName = appName;
            Query = query;
        }
    }
}
