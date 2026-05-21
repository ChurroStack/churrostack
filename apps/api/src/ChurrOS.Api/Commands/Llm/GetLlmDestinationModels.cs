using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmDestinationModels : IRequest<GetLlmDestinationModels, ValueTask<OaiModels>>
    {
        public class GetLlmDestinationModelsBody
        {
            public string Uri { get; private set; }
            public string? ApiKey { get; private set; }

            public GetLlmDestinationModelsBody(string uri, string? apiKey)
            {
                Uri = uri;
                ApiKey = apiKey;
            }
        }

        public long LlmId { get; private set; }
        public GetLlmDestinationModelsBody Body { get; private set; }

        public GetLlmDestinationModels(long llmId, GetLlmDestinationModelsBody body)
        {
            LlmId = llmId;
            Body = body;
        }
    }
}
