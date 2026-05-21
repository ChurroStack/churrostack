using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class TestLlmDestination : IRequest<TestLlmDestination, Task>
    {
        public class TestLlmDestinationBody
        {
            public string Uri { get; private set; }
            public string Model { get; private set; }
            public string? ApiKey { get; private set; }

            public TestLlmDestinationBody(string uri, string? apiKey, string model)
            {
                Uri = uri;
                ApiKey = apiKey;
                Model = model;
            }
        }

        public long LlmId { get; private set; }
        public TestLlmDestinationBody Body { get; private set; }

        public TestLlmDestination(long llmId, TestLlmDestinationBody body)
        {
            LlmId = llmId;
            Body = body;
        }
    }
}
