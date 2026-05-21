using ChurrunKubernetes.Models.Logs;
using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Runtime.CompilerServices;

namespace ChurrunKubernetes.Services
{
    public class KubernetesService
    {
        private readonly Kubernetes _client;
        private readonly IConfiguration _configuration;

        public KubernetesService(IConfiguration configuration)
        {
            KubernetesClientConfiguration config;
            if (!string.IsNullOrEmpty(configuration["Kubernetes:Connection:ClientCertificateKeyData"]))
            {
                config = new KubernetesClientConfiguration()
                {
                    Host = configuration["Kubernetes:Connection:Host"],
                    SkipTlsVerify = bool.Parse(configuration["Kubernetes:Connection:SkipTlsVerify"]!),
                    ClientCertificateData = configuration["Kubernetes:Connection:ClientCertificateData"],
                    ClientCertificateKeyData = configuration["Kubernetes:Connection:ClientCertificateKeyData"],
                };
            }
            else
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            _client = new Kubernetes(config);
            _configuration = configuration;
        }

        public async Task<string[]> GetDeploymentPodsAsync(string @namespace, string deploymentName, CancellationToken cancellationToken)
        {
            var deployment = await _client.ReadNamespacedDeploymentAsync(deploymentName, @namespace, cancellationToken: cancellationToken);
            var selector = deployment.Spec.Selector.MatchLabels;
            var labelSelector = string.Join(",", selector.Select(kv => $"{kv.Key}={kv.Value}"));
            var pods = await _client.ListNamespacedPodAsync(@namespace, labelSelector: labelSelector, cancellationToken: cancellationToken);
            var podNames = pods.Items.Select(p => p.Name());
            return podNames?.ToArray() ?? [];
        }

        public async Task<string[]> GetPodContainersAsync(string @namespace, string podName, CancellationToken cancellationToken)
        {
            var pod = await _client.ReadNamespacedPodAsync(podName, @namespace, cancellationToken: cancellationToken);
            var containerNames = pod.Spec.Containers.Select(c => c.Name);
            return containerNames?.ToArray() ?? [];
        }

        public async IAsyncEnumerable<string> MonitorLogsAsync(string @namespace, string podName, string containerName, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var logStream = await _client.ReadNamespacedPodLogAsync(podName, @namespace, container: containerName, follow: true, tailLines: 10, cancellationToken: cancellationToken);
            using var reader = new StreamReader(logStream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                yield return line;
            }
        }

        public async IAsyncEnumerable<KubernetesDeploymentEvent> MonitorDeploymentAsync(string @namespace, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Watch for pods
            await foreach (var (type, item) in _client.WatchListNamespacedDeploymentAsync(@namespace, cancellationToken: cancellationToken))
            {
                var annotations = item.Metadata.Annotations?.Where(o => o.Key.StartsWith("churrostack.com/"))?.ToDictionary(o => o.Key, o => o.Value)
                    ?? new Dictionary<string, string>();

                var conditions = item.Status.Conditions.Where(o => o.Status?.ToLowerInvariant() == "false").Select(o => new KubernetesDeploymentCondition(o.LastUpdateTime ?? DateTime.Now, o.Type, o.Reason, o.Message)).ToArray();

                yield return new KubernetesDeploymentEvent(item.CreationTimestamp() ?? DateTime.Now, item.Name(), item.Status.Replicas ?? 0, item.Status.AvailableReplicas ?? 0, conditions, annotations);
            }
        }

        public async IAsyncEnumerable<KubernetesGenericEvent> MonitorEventsAsync(string @namespace, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var annotationsCache = new Dictionary<string, IDictionary<string, string>>();
            // Watch for events
            await foreach (var (type, item) in _client.EventsV1.WatchListNamespacedEventAsync(@namespace, cancellationToken: cancellationToken))
            {
                var objectKey = $"{item.Regarding.Kind}/{item.Regarding.Name}";
                if (!annotationsCache.TryGetValue(objectKey, out var annotations))
                {
                    try
                    {
                        switch (item.Regarding.Kind)
                        {
                            case "Pod":
                                var pod = await _client.ReadNamespacedPodAsync(item.Regarding.Name, @namespace);
                                annotations = pod.Metadata.Annotations ?? new Dictionary<string, string>();
                                break;
                            case "Deployment":
                                var deployment = await _client.ReadNamespacedDeploymentAsync(item.Regarding.Name, @namespace);
                                annotations = deployment.Metadata.Annotations ?? new Dictionary<string, string>();
                                break;
                            case "Service":
                                var service = await _client.ReadNamespacedServiceAsync(item.Regarding.Name, @namespace);
                                annotations = service.Metadata.Annotations ?? new Dictionary<string, string>();
                                break;
                            default:
                                // Ignore this event
                                continue;
                        }
                    }
                    catch (HttpOperationException ex)
                    {
                        if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            // Object doesnt exists in kubernetes
                            continue;
                        }
                        throw;
                    }
                    annotations = annotations.Where(o => o.Key.StartsWith("churrostack.com/")).ToDictionary(o => o.Key, o => o.Value);
                    annotationsCache.TryAdd(objectKey, annotations);
                }

                yield return new KubernetesGenericEvent(item.CreationTimestamp() ?? DateTime.Now, item.Reason, item.Type, item.Note, item.Regarding.Kind, item.Regarding.Name, annotations);
            }
        }

        public async Task<KubernetesMetric[]> ScrapeMetricsAsync(string @namespace, CancellationToken cancellationToken)
        {
            var results = new List<KubernetesMetric>();

            var pods = (await _client.ListNamespacedPodAsync(@namespace)).Items.ToDictionary(o => o.Name(), o => o);

            // List Pod metrics via the Metrics API
            var podMetricsList = await _client.GetKubernetesPodsMetricsByNamespaceAsync(@namespace);

            foreach (var metrics in podMetricsList.Items)
            {
                if (!pods.TryGetValue(metrics.Metadata.Name, out var pod))
                {
                    continue;
                }
                string target = pod.Metadata.Name;
                var deploymentId = pod.GetAnnotation("churrostack.com/deployment-id");
                var appName = pod.GetAnnotation("churrostack.com/app-id");

                foreach (var cnt in metrics.Containers)
                {
                    double? cpuUsage = null;
                    double? memoryUsage = null;
                    double? storageUsage = null;
                    double? gpuUsage = null;

                    if (cnt.Usage.TryGetValue("cpu", out var cpuValue))
                    {
                        cpuUsage = cpuValue.ToDouble();
                    }
                    if (cnt.Usage.TryGetValue("memory", out var memoryValue))
                    {
                        memoryUsage = memoryValue.ToDouble();
                    }
                    results.Add(new KubernetesMetric(deploymentId, appName, $"{target}:{cnt.Name}", cpuUsage, memoryUsage, storageUsage, gpuUsage));
                }

            }

            return results.ToArray();
        }

        public async Task DeleteDeploymentManifest(string name, string @namespace)
        {
            await _client.DeleteNamespacedDeploymentAsync(name, @namespace);
        }

        public async Task DeleteServiceManifest(string name, string @namespace)
        {
            await _client.DeleteNamespacedServiceAsync(name, @namespace);
        }

        public async Task DeletePvcManifest(string name, string @namespace)
        {
            await _client.DeleteNamespacedPersistentVolumeClaimAsync(name, @namespace);
        }

        public async Task DeleteConfigMapsManifest(string name, string @namespace)
        {
            await _client.DeleteNamespacedConfigMapAsync(name, @namespace);
        }

        public async Task<string[]> GetPvcsManifests(string @namespace, (string AnnotationKey, string? AnnotationValue)[] filter)
        {
            var result = await _client.ListNamespacedPersistentVolumeClaimAsync(@namespace);

            IEnumerable<V1PersistentVolumeClaim> deployments = result.Items;
            foreach (var filterItem in filter)
            {
                deployments = deployments
                    .Where(d => d.Metadata?.Annotations != null &&
                            d.Metadata.Annotations.ContainsKey(filterItem.AnnotationKey) &&
                            (filterItem.AnnotationValue == null || d.Metadata.Annotations[filterItem.AnnotationKey] == filterItem.AnnotationValue));
            }

            return deployments.Select(o => o.Name()).ToArray();
        }

        public async Task<string[]> GetDeploymentsManifests(string @namespace, (string AnnotationKey, string? AnnotationValue)[] filter)
        {
            var result = await _client.ListNamespacedDeploymentAsync(@namespace);

            IEnumerable<V1Deployment> deployments = result.Items;
            foreach (var filterItem in filter)
            {
                deployments = deployments
                    .Where(d => d.Metadata?.Annotations != null &&
                            d.Metadata.Annotations.ContainsKey(filterItem.AnnotationKey) &&
                            (filterItem.AnnotationValue == null || d.Metadata.Annotations[filterItem.AnnotationKey] == filterItem.AnnotationValue));
            }

            return deployments.Select(o => o.Name()).ToArray();
        }

        public async Task<string[]> GetServicesManifests(string @namespace, (string AnnotationKey, string? AnnotationValue)[] filter)
        {
            var result = await _client.ListNamespacedServiceAsync(@namespace);

            IEnumerable<V1Service> services = result.Items;
            foreach (var filterItem in filter)
            {
                services = services
                    .Where(d => d.Metadata?.Annotations != null &&
                            d.Metadata.Annotations.ContainsKey(filterItem.AnnotationKey) &&
                            (filterItem.AnnotationValue == null || d.Metadata.Annotations[filterItem.AnnotationKey] == filterItem.AnnotationValue));
            }

            return services.Select(o => o.Name()).ToArray();
        }

        public async Task<string[]> GetConfigMapsManifests(string @namespace, (string AnnotationKey, string? AnnotationValue)[] annotations)
        {
            var result = await _client.ListNamespacedConfigMapAsync(@namespace);

            IEnumerable<V1ConfigMap> configMaps = result.Items;
            foreach (var filterItem in annotations)
            {
                configMaps = configMaps
                    .Where(d => d.Metadata?.Annotations != null &&
                            d.Metadata.Annotations.ContainsKey(filterItem.AnnotationKey) &&
                            (filterItem.AnnotationValue == null || d.Metadata.Annotations[filterItem.AnnotationKey] == filterItem.AnnotationValue));
            }

            return configMaps.Select(o => o.Name()).ToArray();
        }

        public async Task ScaleManifestAsync(string deploymentName, string @namespace, int replicas)
        {
            // Get current scale
            var scale = await _client.ReadNamespacedDeploymentScaleAsync(
                deploymentName,
                @namespace
            );

            // Update replicas
            scale.Spec.Replicas = replicas;

            // Apply scale
            await _client.ReplaceNamespacedDeploymentScaleAsync(
                scale,
                deploymentName,
                @namespace
            );
        }

        public async Task ApplyYamlManifests(string manifests, IDictionary<string, string> annotations)
        {
            var documents = manifests.Split(["---"], StringSplitOptions.RemoveEmptyEntries);
            foreach (var doc in documents)
            {
                var obj = KubernetesYaml.Deserialize<KubernetesObject>(doc);
                switch (obj.Kind)
                {
                    case "Namespace":
                        var ns = KubernetesYaml.Deserialize<V1Namespace>(doc);
                        if (annotations is not null)
                        {
                            foreach (var annotation in annotations)
                            {
                                ns.SetAnnotation(annotation.Key, annotation.Value);
                            }
                        }
                        try
                        {
                            await _client.CreateNamespaceAsync(ns);
                        }
                        catch (HttpOperationException ex)
                        {
                            if (ex.Response.StatusCode != System.Net.HttpStatusCode.Conflict)
                            {
                                throw;
                            }
                        }
                        break;
                    case "Deployment":
                        var deployment = KubernetesYaml.Deserialize<V1Deployment>(doc);
                        if (annotations is not null)
                        {
                            foreach (var annotation in annotations)
                            {
                                deployment.SetAnnotation(annotation.Key, annotation.Value);
                                deployment.Spec.Template.SetAnnotation(annotation.Key, annotation.Value);
                            }
                        }
                        var @namespace = string.IsNullOrWhiteSpace(deployment.Metadata.NamespaceProperty) ? _configuration["Kubernetes:Namespace"] : deployment.Metadata.NamespaceProperty;
                        try
                        {
                            await _client.CreateNamespacedDeploymentAsync(deployment, deployment.Metadata.NamespaceProperty);
                        }
                        catch (HttpOperationException ex)
                        {
                            if (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
                            {
                                await _client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Metadata.NamespaceProperty);
                            }
                            else
                            {
                                throw;
                            }
                        }
                        break;
                    case "Service":
                        var service = KubernetesYaml.Deserialize<V1Service>(doc);
                        if (annotations is not null)
                        {
                            foreach (var annotation in annotations)
                            {
                                service.SetAnnotation(annotation.Key, annotation.Value);
                            }
                        }
                        try
                        {
                            await _client.CreateNamespacedServiceAsync(service, service.Metadata.NamespaceProperty);
                        }
                        catch (HttpOperationException ex)
                        {
                            if (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
                            {
                                await _client.ReplaceNamespacedServiceAsync(service, service.Name(), service.Metadata.NamespaceProperty);
                            }
                            else
                            {
                                throw;
                            }
                        }
                        break;
                    case "ConfigMap":
                        var configMap = KubernetesYaml.Deserialize<V1ConfigMap>(doc);
                        if (annotations is not null)
                        {
                            foreach (var annotation in annotations)
                            {
                                configMap.SetAnnotation(annotation.Key, annotation.Value);
                            }
                        }
                        try
                        {
                            await _client.CreateNamespacedConfigMapAsync(configMap, configMap.Metadata.NamespaceProperty);
                        }
                        catch (HttpOperationException ex)
                        {
                            if (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
                            {
                                await _client.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Metadata.NamespaceProperty);
                            }
                            else
                            {
                                throw;
                            }
                        }
                        break;
                    case "PersistentVolumeClaim":
                        var pvc = KubernetesYaml.Deserialize<V1PersistentVolumeClaim>(doc);
                        if (annotations is not null)
                        {
                            foreach (var annotation in annotations)
                            {
                                pvc.SetAnnotation(annotation.Key, annotation.Value);
                            }
                        }
                        try
                        {
                            await _client.CreateNamespacedPersistentVolumeClaimAsync(pvc, pvc.Metadata.NamespaceProperty);
                        }
                        catch (HttpOperationException ex)
                        {
                            if (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
                            {
                                var patch = new V1Patch(
                                    new
                                    {
                                        spec = new
                                        {
                                            resources = new
                                            {
                                                requests = new
                                                {
                                                    storage = pvc.Spec.Resources.Requests["storage"]
                                                }
                                            }
                                        }
                                    },
                                    V1Patch.PatchType.MergePatch // or JsonPatch if you prefer
                                );

                                await _client.PatchNamespacedPersistentVolumeClaimAsync(patch, pvc.Name(), pvc.Metadata.NamespaceProperty);
                            }
                            else
                            {
                                throw;
                            }
                        }
                        break;
                    // Add more cases for other Kubernetes resource kinds as needed
                    default:
                        throw new NotSupportedException($"Kind '{obj.Kind}' is not supported.");
                }
            }
        }
    }
}
