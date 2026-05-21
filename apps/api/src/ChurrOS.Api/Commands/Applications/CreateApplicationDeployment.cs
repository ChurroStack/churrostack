using ChurrOS.Api.Models.Dtos.Deployment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class CreateApplicationDeployment : IRequest<CreateApplicationDeployment, ValueTask<DeploymentSummary?>>
    {
        public class CreateApplicationDeploymentBody
        {
            public string IdentityName { get; private set; }

            public CreateApplicationDeploymentBody(string identityName)
            {
                IdentityName = identityName;
            }
        }

        public string Name { get; private set; }

        public CreateApplicationDeploymentBody Body { get; private set; }

        public CreateApplicationDeployment(string name, CreateApplicationDeploymentBody body)
        {
            Name = name;
            Body = body;
        }
    }
}
