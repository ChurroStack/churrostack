using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationUsage : IRequest<GetApplicationUsage, ValueTask<QueryResult<ApplicationUsageItem>>>
    {
        public class UsageQueryRequest : QueryRequest
        {
            public DateTimeOffset? From { get; set; }
            public DateTimeOffset? To { get; set; }
            public string? GroupBy { get; set; }
            public string? OrderBy { get; set; }

            public UsageQueryRequest()
            {
            }

            public UsageQueryRequest(DateTimeOffset? from, DateTimeOffset? to, string? groupBy, string? orderBy, int? page = 1, int? pageSize = 25, string? search = null) : base(page, pageSize, search)
            {
                From = from;
                To = to;
                GroupBy = groupBy;
                OrderBy = orderBy;
            }
        }

        public string AppName { get; private set; }

        public UsageQueryRequest Query { get; private set; }

        public GetApplicationUsage(string appName, UsageQueryRequest query)
        {
            AppName = appName;
            Query = query;
        }
    }
}
