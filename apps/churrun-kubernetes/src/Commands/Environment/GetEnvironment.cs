using ChurrunKubernetes.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Environment
{
    public class GetEnvironment : IRequest<GetEnvironment, ValueTask<EnvironmentDefinition>>
    {

        public GetEnvironment()
        {

        }
    }
}
