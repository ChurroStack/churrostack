using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Applications
{
    public class CreateApplication : IRequest<CreateApplication, ValueTask<ApplicationItem>>
    {
        public class ApplicationExtensionBody
        {
            public string Name { get; protected set; }
            public bool Enabled { get; protected set; }
            public string Template { get; protected set; }
            public IDictionary<string, string[]> Parameters { get; protected set; }

            public ApplicationExtensionBody(string name, bool enabled, string template, IDictionary<string, string[]> parameters)
            {
                Name = name;
                Enabled = enabled;
                Template = template;
                Parameters = parameters;
            }
        }
        public class CreateApplicationBody
        {
            [Required]
            public string Name { get; private set; }

            [Required]
            public ApplicationMode Mode { get; private set; }

            [Required]
            public string Template { get; private set; }

            [Required]
            public string Environment { get; private set; }

            public ApplicationExtensionBody[]? Extensions { get; private set; }

            public ApplicationEnvironmentVariable[]? Variables { get; private set; }

            public JsonElement? Metadata { get; protected set; }

            public CreateApplicationBody(string name, ApplicationMode mode, string template, string environment, ApplicationExtensionBody[]? extensions, ApplicationEnvironmentVariable[]? variables, JsonElement? metadata)
            {
                Name = name;
                Mode = mode;
                Template = template;
                Environment = environment;
                Extensions = extensions;
                Variables = variables;
                Metadata = metadata;
            }
        }

        public CreateApplicationBody Body { get; private set; }

        public CreateApplication(CreateApplicationBody body)
        {
            Body = body;
        }
    }
}
