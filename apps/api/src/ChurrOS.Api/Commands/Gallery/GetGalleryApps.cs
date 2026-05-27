using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Gallery;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Gallery
{
    public class GetGalleryApps : IRequest<GetGalleryApps, ValueTask<QueryResult<GalleryAppSummary>>>
    {
        public class GalleryAppsQueryRequest : QueryRequest
        {
            public string[]? Tags { get; set; }

            public string? Environment { get; set; }

            public string? CreatedBy { get; set; }

            public GalleryAppsQueryRequest() : base() { }

            public GalleryAppsQueryRequest(int? page = DefaultPage, int? pageSize = DefaultPageSize, string? search = null, string[]? tags = null, string? environment = null, string? createdBy = null) : base(page, pageSize, search)
            {
                Tags = tags;
                Environment = environment;
                CreatedBy = createdBy;
            }
        }

        public GalleryAppsQueryRequest Query { get; private set; }

        public GetGalleryApps(GalleryAppsQueryRequest query)
        {
            Query = query;
        }
    }
}
