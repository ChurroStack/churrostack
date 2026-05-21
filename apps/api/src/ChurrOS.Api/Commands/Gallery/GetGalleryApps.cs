using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Gallery;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Gallery
{
    public class GetGalleryApps : IRequest<GetGalleryApps, ValueTask<QueryResult<GalleryAppSummary>>>
    {
        public QueryRequest Query { get; private set; }

        public GetGalleryApps(QueryRequest query)
        {
            Query = query;
        }
    }
}
