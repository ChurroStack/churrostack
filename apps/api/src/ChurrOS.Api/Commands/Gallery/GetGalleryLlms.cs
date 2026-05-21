using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Gallery;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Gallery
{
    public class GetGalleryLlms : IRequest<GetGalleryLlms, ValueTask<QueryResult<GalleryLlmSummary>>>
    {
        public QueryRequest Query { get; private set; }

        public GetGalleryLlms(QueryRequest query)
        {
            Query = query;
        }
    }
}
