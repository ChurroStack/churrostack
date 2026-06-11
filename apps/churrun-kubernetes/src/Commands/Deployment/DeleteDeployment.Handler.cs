using ChurrunKubernetes.Data;
using ChurrunKubernetes.Models.Dtos.Exceptions;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Services.Share;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class DeleteDeploymentHandler : IRequestHandler<DeleteDeployment, Task>
    {
        private readonly KubernetesService _kubernetesService;
        private readonly IConfiguration _configuration;
        private readonly ChurrunDbContext _dbContext;
        private readonly ProxyConfigurationProvider _proxyConfigurationProvider;

        public DeleteDeploymentHandler(KubernetesService kubernetesService, IConfiguration configuration, ChurrunDbContext dbContext, ProxyConfigurationProvider proxyConfigurationProvider)
        {
            _kubernetesService = kubernetesService;
            _configuration = configuration;
            _dbContext = dbContext;
            _proxyConfigurationProvider = proxyConfigurationProvider;
        }

        public async Task Handle(DeleteDeployment request, CancellationToken cancellationToken)
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
                await _kubernetesService.DeleteDeploymentManifest(deploymentName, @namespace);
            }
            var services = await _kubernetesService.GetServicesManifests(@namespace, annotations);
            foreach (var serviceName in services)
            {
                exists = true;
                await _kubernetesService.DeleteServiceManifest(serviceName, @namespace);
            }
            var pvcs = await _kubernetesService.GetPvcsManifests(@namespace, annotations);
            foreach (var pvc in pvcs)
            {
                exists = true;
                await _kubernetesService.DeletePvcManifest(pvc, @namespace);
            }
            // PersistentVolumes are cluster-scoped (not deleted with the namespace); remove the
            // ones we created for this deployment's hostPath storage so they don't leak.
            var pvs = await _kubernetesService.GetPvsManifests(annotations);
            foreach (var pv in pvs)
            {
                exists = true;
                await _kubernetesService.DeletePvManifest(pv);
            }
            var cms = await _kubernetesService.GetConfigMapsManifests(@namespace, annotations);
            foreach (var cm in cms)
            {
                exists = true;
                await _kubernetesService.DeleteConfigMapsManifest(cm, @namespace);
            }
            if (!exists)
            {
                throw new HttpException(404, $"Deployment with name '{request.Name}' not found.");
            }
            var deployment = _dbContext.Set<Domain.Deployment>().Find(request.Name);
            if (deployment is not null)
            {
                _dbContext.Set<Domain.Deployment>().Remove(deployment);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _proxyConfigurationProvider.RemoveProxy(deployment.Name);
                _proxyConfigurationProvider.Reload();
            }
        }
    }
}
