namespace ChurrOS.Api.Models.Dtos.Deployment
{
    /// <summary>
    /// Reduces the deployments belonging to an application into a single
    /// (provision, execution) status pair using the same priority rules across
    /// every handler that surfaces application status.
    /// </summary>
    public static class DeploymentStatusAggregator
    {
        public static (DeploymentProvisionStatus Provision, DeploymentExecutionStatus Execution) Aggregate(
            IEnumerable<(DeploymentProvisionStatus Provision, DeploymentExecutionStatus Execution)> deployments)
        {
            var provision = DeploymentProvisionStatus.Pending;
            var execution = DeploymentExecutionStatus.Stopped;

            foreach (var (p, e) in deployments)
            {
                switch (p)
                {
                    case DeploymentProvisionStatus.Provisioning:
                        if (provision == DeploymentProvisionStatus.Pending)
                            provision = DeploymentProvisionStatus.Provisioning;
                        break;
                    case DeploymentProvisionStatus.Provisioned:
                        if (provision != DeploymentProvisionStatus.Failed)
                            provision = DeploymentProvisionStatus.Provisioned;
                        break;
                    case DeploymentProvisionStatus.Failed:
                        provision = DeploymentProvisionStatus.Failed;
                        break;
                }
                switch (e)
                {
                    case DeploymentExecutionStatus.Starting:
                        if (execution == DeploymentExecutionStatus.Stopped)
                            execution = DeploymentExecutionStatus.Starting;
                        break;
                    case DeploymentExecutionStatus.Running:
                        execution = DeploymentExecutionStatus.Running;
                        break;
                    case DeploymentExecutionStatus.Stopping:
                        if (execution != DeploymentExecutionStatus.Running)
                            execution = DeploymentExecutionStatus.Stopping;
                        break;
                }
            }

            return (provision, execution);
        }
    }
}
