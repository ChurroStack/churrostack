using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class ApplicationMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // EnvironmentName/TemplateName are populated via Mapster auto-flattening
            // from the anonymous projection in GetApplicationsHandler (o.Environment.Name,
            // o.Template.Name). Adding explicit Map() rules here would be dead code because
            // the handler maps from an anonymous type, not Domain.Application.
            TypeAdapterConfig<Domain.Application, ApplicationSummary>
                .NewConfig()
                .Map(o => o.ProvisionStatus, o => DeploymentProvisionStatus.Provisioning)
                .Map(o => o.ExecutionStatus, o => DeploymentExecutionStatus.Stopped)
                .Map(o => o.Metrics, o => new Dictionary<string, double>());

            TypeAdapterConfig<Domain.Application, ApplicationItem>
                .NewConfig()
                .Map(o => o.EnvironmentName, o => o.Environment!.Name)
                .Map(dest => dest.Members, src => src.Acl!.Members);

            TypeAdapterConfig<Domain.ApplicationEvent, ApplicationEventItem>
                .NewConfig();

            TypeAdapterConfig<Domain.ApplicationDeployment, ApplicationDeploymentItem>
                .NewConfig()
                .Map(o => o.Metrics, o => new Dictionary<string, double>());

            TypeAdapterConfig<Domain.ApplicationExtension, ApplicationExtensionItem>
                .NewConfig();

            TypeAdapterConfig<Domain.ApplicationExtension, DeploymentExtensionRequestItem>
                .NewConfig()
                .Map(o => o.Template, o => o.Template!.Name);

            TypeAdapterConfig<PortDefinition, PortDefinitionItem>
                .NewConfig();

            TypeAdapterConfig<Domain.ApplicationSchedule, ApplicationScheduleItem>
                .NewConfig();
        }
    }
}
