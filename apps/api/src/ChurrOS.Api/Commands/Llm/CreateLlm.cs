using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class CreateLlm : IRequest<CreateLlm, ValueTask<LlmItem>>
    {
        public class CreateLlmBody
        {
            public string[] Names { get; private set; }

            public CreateLlmBody(string[] names)
            {
                Names = names;
            }
        }

        public CreateLlmBody Body { get; private set; }

        public CreateLlm(CreateLlmBody body)
        {
            Body = body;
        }
    }
}
