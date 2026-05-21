import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Item, ItemActions, ItemContent, ItemDescription, ItemMedia, ItemTitle } from '@/components/ui/item';
import { Spinner } from '@/components/ui/spinner';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { formatBytes, formatPercent } from '@/extensions';
import {
  getApplicationStatus,
  useDeleteApplication,
  useDeployApplication,
  useGetApplicationDeployments,
  useStartApplication,
  useStopApplication,
  type ApplicationDeploymentItem,
  type ApplicationItem
} from '@/hooks/data/applications';
import {
  AlertCircle,
  AppWindow,
  CloudUpload,
  Cpu,
  EllipsisVertical,
  MemoryStick,
  Play,
  Plus,
  RefreshCcw,
  Square,
  Trash2
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppStatus } from '../common/app-status';
import { useNotifications } from '@/services/notification-service';
import { NewDeploymentDialog } from '../dialogs/new-deployment';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { ConfirmDialog } from '@/components/confirm-dialog';

const DeploymentActions = ({
  deployment,
  app,
  refresh
}: {
  deployment: ApplicationDeploymentItem;
  app: ApplicationItem;
  refresh: () => void;
}) => {
  const { t } = useTranslation();

  const { isFetching: isDeploying, error: deployError, postAsync: deployAsync } = useDeployApplication(app.name ?? '');
  const { postAsync: startAsync, isFetching: isStarting, error: startError } = useStartApplication(app.name ?? '');
  const { postAsync: stopAsync, isFetching: isStopping, error: stopError } = useStopApplication(app.name ?? '');
  const { deleteAsync: deleteAsync, isFetching: isDeleting, error: deleteError } = useDeleteApplication();
  const [open, setOpen] = useState(false);

  const appStatus = useMemo(
    () => getApplicationStatus(deployment.provisionStatus, deployment.executionStatus),
    [app.name, deployment.provisionStatus, deployment.executionStatus]
  );

  const isFetching = isDeploying || isStarting || isStopping || isDeleting;

  const onDeployApplication = async (deploymentName: string) => {
    await deployAsync({}, undefined, `deployment=${deploymentName}`);
  };

  return (
    <>
      {startError && (
        <Tooltip>
          <TooltipTrigger>
            <AlertCircle className="size-4 text-destructive" />
          </TooltipTrigger>
          <TooltipContent>{startError}</TooltipContent>
        </Tooltip>
      )}
      {stopError && (
        <Tooltip>
          <TooltipTrigger>
            <AlertCircle className="size-4 text-destructive" />
          </TooltipTrigger>
          <TooltipContent>{stopError}</TooltipContent>
        </Tooltip>
      )}
      {deployError && (
        <Tooltip>
          <TooltipTrigger>
            <AlertCircle className="size-4 text-destructive" />
          </TooltipTrigger>
          <TooltipContent>{deployError}</TooltipContent>
        </Tooltip>
      )}
      {deleteError && (
        <Tooltip>
          <TooltipTrigger>
            <AlertCircle className="size-4 text-destructive" />
          </TooltipTrigger>
          <TooltipContent>{deleteError}</TooltipContent>
        </Tooltip>
      )}
      {(appStatus === 'failed' || appStatus === 'pending' || deployment.deployedAt < app.modifiedAt) && (
        <Button size="sm" disabled={isFetching} onClick={() => onDeployApplication(deployment?.name ?? '')}>
          {isDeploying ? <Spinner /> : <CloudUpload />} {t('Deploy')}
        </Button>
      )}
      {(appStatus === 'running' || appStatus === 'starting' || appStatus === 'provisioning') && (
        <Button
          onClick={async () => {
            await stopAsync({}, undefined, `deployment=${deployment.name}`);
          }}
          size="sm"
          disabled={isFetching}
          variant="secondary">
          {isStopping ? <Spinner /> : <Square />} {t('Stop')}
        </Button>
      )}
      {appStatus === 'stopped' && (
        <Button
          onClick={async () => {
            await startAsync({}, undefined, `deployment=${deployment.name}`);
          }}
          size="sm"
          disabled={isFetching}
          variant="secondary">
          {isStarting ? <Spinner /> : <Play />} {t('Start')}
        </Button>
      )}
      {(appStatus === 'provisioning' || appStatus === 'starting') && <Spinner />}
      <DropdownMenu open={open} onOpenChange={setOpen}>
        <DropdownMenuTrigger asChild>
          <div className="width-10">
            <EllipsisVertical size="14" />
          </div>
        </DropdownMenuTrigger>
        <DropdownMenuContent className="w-56" align="start">
          <ConfirmDialog
            title={t('Deploy application')}
            description={t('Are you sure you want to deploy this application?')}
            acceptText={t('Deploy')}
            onAccept={async () => {
              onDeployApplication(deployment.name);
              setOpen(false);
            }}
            onCancel={() => setOpen(false)}>
            <DropdownMenuItem onSelect={(e) => e.preventDefault()}>
              <CloudUpload /> {t('Deploy')}
            </DropdownMenuItem>
          </ConfirmDialog>
          <DropdownMenuSeparator />
          <ConfirmDialog
            title={t('Delete application')}
            description={t('Are you sure you want to delete this application?')}
            acceptText={t('Delete')}
            onAccept={async () => {
              setOpen(false);
              await deleteAsync(app.name, `deployment=${deployment.name}`);
              refresh?.();
            }}
            onCancel={() => setOpen(false)}>
            <DropdownMenuItem className="text-destructive" onSelect={(e) => e.preventDefault()}>
              <Trash2 className="text-destructive" /> {t('Delete')}
            </DropdownMenuItem>
          </ConfirmDialog>
        </DropdownMenuContent>
      </DropdownMenu>
    </>
  );
};

const DeploymentsPanel = ({ application }: { application: ApplicationItem }) => {
  const { t } = useTranslation();
  const { fetchAsync, data, error, isFetching } = useGetApplicationDeployments(application.name ?? '');
  const { subscribe } = useNotifications();
  const [showNewDeploymentDialog, setShowNewDeploymentDialog] = useState(false);

  useEffect(() => {
    return subscribe((message) => {
      if (message.target === 'deployment' && application.name && message.name.startsWith(application.name)) {
        load();
      }
    });
  }, [application.name]);

  const load = async () => {
    await fetchAsync('');
  };

  useEffect(() => {
    load();
  }, [application.name]);

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Deployments')}</h3>
        </div>
        <div className="flex flex-row items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => load()} disabled={isFetching}>
            {isFetching ? <Spinner /> : <RefreshCcw />} {t('Refresh')}
          </Button>

          <Button size="sm" onClick={() => setShowNewDeploymentDialog(true)}>
            <Plus /> {t('Add deployment')}
          </Button>
        </div>
      </div>
      {error && (
        <div className="p-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error updating application ports')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        {showNewDeploymentDialog && (
          <NewDeploymentDialog
            appName={application.name}
            showDialog={showNewDeploymentDialog}
            onClose={() => setShowNewDeploymentDialog(false)}
            reload={load}
          />
        )}
        {data?.items.map((deployment, index) => (
          <Item variant="outline">
            <ItemMedia key={`deployment-${index}`}>
              <AppWindow />
            </ItemMedia>
            <ItemContent>
              <ItemTitle>
                {deployment?.owner?.name
                  ? deployment.owner.displayName && deployment.owner.displayName !== deployment.owner.name
                    ? `${deployment.owner.displayName} (${deployment.owner.name})`
                    : `${deployment.owner.name}`
                  : deployment.name}
              </ItemTitle>
              <ItemDescription>
                <div className="flex flex-row gap-4 justify-start w-full items-center text-muted-foreground text-xs">
                  <Tooltip>
                    <TooltipTrigger>
                      <div className="flex flex-row gap-1">
                        <Cpu className="size-4" />{' '}
                        {deployment.metrics?.cpu_usage &&
                        deployment.metrics?.cpu_usage > 0 &&
                        deployment.executionStatus !== 'stopped'
                          ? formatPercent(deployment.metrics?.cpu_usage ?? 0)
                          : 0}
                        %
                      </div>
                    </TooltipTrigger>
                    <TooltipContent>{t('CPU Usage (%)')}</TooltipContent>
                  </Tooltip>
                  <Tooltip>
                    <TooltipTrigger>
                      <div className="flex flex-row gap-1">
                        <MemoryStick className="size-4 rotate-135" />{' '}
                        {formatBytes(
                          deployment.metrics?.memory_usage &&
                            deployment.metrics?.memory_usage > 0 &&
                            deployment.executionStatus !== 'stopped'
                            ? parseFloat(`${deployment.metrics?.memory_usage ?? '0'}`)
                            : 0
                        ) ?? '-'}
                      </div>
                    </TooltipTrigger>
                    <TooltipContent>{t('Memory Usage (Bytes)')}</TooltipContent>
                  </Tooltip>
                  <AppStatus status={getApplicationStatus(deployment.provisionStatus, deployment.executionStatus)} />
                  <span className="text-sm text-muted-foreground">{deployment.name}</span>
                </div>
              </ItemDescription>
            </ItemContent>
            <ItemActions>
              <div className="flex flex-row gap-2 items-center">
                <DeploymentActions deployment={deployment} app={application} refresh={() => load()} />
              </div>
            </ItemActions>
          </Item>
        ))}
      </div>
    </div>
  );
};

export default DeploymentsPanel;
