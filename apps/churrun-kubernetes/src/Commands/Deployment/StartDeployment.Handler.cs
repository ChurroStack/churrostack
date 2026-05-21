using ChurrunKubernetes.Models.Dtos.Exceptions;
using ChurrunKubernetes.Services;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class StartDeploymentHandler : IRequestHandler<StartDeployment, Task>
    {
        private readonly KubernetesService _kubernetesService;
        private readonly IConfiguration _configuration;

        public StartDeploymentHandler(KubernetesService kubernetesService, IConfiguration configuration)
        {
            _kubernetesService = kubernetesService;
            _configuration = configuration;
        }

        public async Task Handle(StartDeployment request, CancellationToken cancellationToken)
        {
            bool exists = false;
            var @namespace = _configuration["Kubernetes:Namespace"]!;
            (string AnnotationKey, string? AnnotationValue)[] annotations = [("churrostack.com/deployment-id", request.Name)];
            if (request.Hash is not null)
            {
                annotations = annotations.Append(("churrostack.com/app-hash", Convert.ToBase64String(request.Hash))).ToArray();
            }

            var deployments = await _kubernetesService.GetDeploymentsManifests(@namespace, annotations);
            foreach (var deploymentName in deployments)
            {
                exists = true;
                await _kubernetesService.ScaleManifestAsync(deploymentName, @namespace, request.Replicas);
            }
            if (!exists)
            {
                throw new HttpException(404, $"Deployment with name '{request.Name}' not found.");
            }
        }
    }
}
