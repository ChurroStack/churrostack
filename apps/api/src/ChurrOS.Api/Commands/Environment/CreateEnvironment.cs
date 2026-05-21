using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Commands.Environment
{
    public class CreateEnvironment : IRequest<CreateEnvironment, ValueTask<EnvironmentItem>>
    {
        public class CreateEnvironmentBody
        {
            [Required]
            public string Name { get; private set; }

            public CreateEnvironmentBody(string name)
            {
                Name = name;
            }
        }

        public CreateEnvironmentBody Body { get; private set; }

        public CreateEnvironment(CreateEnvironmentBody body)
        {
            Body = body;
        }
    }
}
