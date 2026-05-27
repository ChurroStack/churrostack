using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class UpdateEnvironment : IRequest<UpdateEnvironment, ValueTask<EnvironmentItem>>
    {
        public class UpdateEnvironmentBody
        {
            public MemberItem[]? Members { get; set; }

            public string[]? Tags { get; set; }

            public UpdateEnvironmentBody(MemberItem[]? members, string[]? tags = null)
            {
                Members = members;
                Tags = tags;
            }
        }

        public string Name { get; private set; }

        public UpdateEnvironmentBody Body { get; private set; }

        public bool Validate { get; private set; }

        public UpdateEnvironment(string name, UpdateEnvironmentBody body, bool validate)
        {
            Name = name;
            Body = body;
            Validate = validate;
        }
    }
}
