using ChurrunKubernetes.Models.Dtos.Deployment;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class CreateDeployment : IRequest<CreateDeployment, ValueTask<DeploymentSummary>>
    {
        public class EnvironmentVariableBody
        {
            public string Name { get; private set; }

            public string Value { get; internal set; }

            public EnvironmentVariableBody(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }


        public class ExtensionBody
        {
            public string Name { get; private set; }

            public string Template { get; internal set; }

            public IDictionary<string, string[]>? Parameters { get; private set; }

            public ExtensionBody(string name, string template, IDictionary<string, string[]>? parameters)
            {
                Name = name;
                Template = template;
                Parameters = parameters;
            }
        }

        public class CreateDeploymentBody
        {
            public string Name { get; private set; }

            public string AppName { get; private set; }

            public string Template { get; internal set; }

            public int? Replicas { get; private set; }

            public DeploymentSizeItem? Size { get; private set; }

            public IDictionary<string, string[]>? Parameters { get; private set; }
            public EnvironmentVariableBody[]? Variables { get; private set; }

            public ExtensionBody[]? Extensions { get; private set; }
            public PortDefinition[]? Ports { get; set; }

            public CreateDeploymentBody(string name, string appName, string template, int? replicas, DeploymentSizeItem? size, IDictionary<string, string[]>? parameters, ExtensionBody[]? extensions, PortDefinition[]? ports, EnvironmentVariableBody[]? variables)
            {
                Name = name;
                AppName = appName;
                Template = template;
                Replicas = replicas;
                Size = size;
                Parameters = parameters;
                Extensions = extensions;
                Ports = ports;
                Variables = variables;
            }
        }

        public CreateDeploymentBody Body { get; private set; }

        public bool Dry { get; private set; }

        public CreateDeployment(CreateDeploymentBody body, bool dry = false)
        {
            Body = body;
            Dry = dry;
        }
    }
}
