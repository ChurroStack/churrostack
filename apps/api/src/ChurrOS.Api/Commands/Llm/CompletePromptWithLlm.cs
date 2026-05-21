using DispatchR.Abstractions.Stream;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Llm
{
    public class CompletePromptWithLlm : IStreamRequest<CompletePromptWithLlm, JsonElement>
    {
        public JsonElement Body { get; private set; }
        public bool IsStream { get; private set; }
        public bool IsCompletion { get; private set; }
        public string? XUserId { get; private set; }

        public CompletePromptWithLlm(JsonElement body, bool isStream, bool isCompletion, string? xUserId)
        {
            Body = body;
            IsStream = isStream;
            XUserId = xUserId;
            IsCompletion = isCompletion;
        }
    }
}
