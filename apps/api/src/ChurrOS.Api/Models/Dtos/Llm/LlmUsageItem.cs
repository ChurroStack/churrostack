namespace ChurrOS.Api.Models.Dtos.Llm
{
    public class LlmUsageItem
    {
        public string Name { get; set; }
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long Completions { get; set; }

        public LlmUsageItem(string name, long promptTokens, long completionTokens, long completions)
        {
            Name = name;
            PromptTokens = promptTokens;
            CompletionTokens = completionTokens;
            Completions = completions;
        }
    }
}
