
namespace ChurrOS.Api.Models.Dtos
{
    public class QueryRequest
    {
        protected const int DefaultPage = 1;
        protected const int DefaultPageSize = 25;
        public string? Search { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public QueryRequest() : this(DefaultPage, DefaultPageSize, null)
        {
        }

        public QueryRequest(int? page = DefaultPage, int? pageSize = DefaultPageSize, string? search = null)
        {
            Page = page ?? DefaultPage;
            PageSize = pageSize ?? DefaultPageSize;
            Search = search;
        }

        public IQueryable<T>? ApplyPaginationTo<T>(IQueryable<T> query)
        {
            return query
                .Skip((Page - 1) * PageSize)
                .Take(PageSize);
        }
    }
}
