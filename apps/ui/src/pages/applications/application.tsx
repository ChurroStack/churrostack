import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { renderIcon } from '@/extensions';
import {
  getApplicationStatus,
  useDeleteApplication,
  useDeployApplication,
  useGetApplication,
  useStartApplication,
  useStopApplication,
  type DeploymentExecutionStatus,
  type DeploymentProvisionStatus
} from '@/hooks/data/applications';
import {
  AlertCircle,
  AppWindow,
  CalendarCheck,
  ChartNoAxesCombined,
  CloudUpload,
  Cog,
  ExternalLink,
  Keyboard,
  Layers,
  Logs,
  Play,
  Share2,
  Square,
  TextSearch,
  TriangleAlert,
  UserLock,
  Variable
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import ApplicationContextMenu from './menus/application-menu';
import { useApplicationService } from '@/services/application-services';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useNotifications } from '@/services/notification-service';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Item, ItemContent, ItemDescription, ItemTitle } from '@/components/ui/item';
import EventsConsole from './panels/events-panel';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import ConsolePanel from './panels/console-panel';
import MonitorPanel from './panels/monitor-panel';
import EnvironmentVariablesPanel from './panels/variables-panel';
import SettingsPanel from './panels/settings-panel';
import AccessPanel from './panels/members-panel';
import PortsPanel from './panels/ports-panel';
import { AppStatus } from './common/app-status';
import TracesPanel from './panels/traces-panel';
import DeploymentsPanel from './panels/deployments-panel';
import { SchedulesPanel } from './panels/schedules-panel';

const Application = () => {
  const { t } = useTranslation();
  const { id } = useParams();
  const { reload } = useApplicationService();
  const { fetchAsync, data, isFetching, error } = useGetApplication(id);
  const [app, setApp] = useState(data);
  const navigate = useNavigate();
  const { error: deleteError, deleteAsync } = useDeleteApplication();
  const {
    isFetching: isDeploying,
    error: deployError,
    postAsync: deployAsync
  } = useDeployApplication(data?.name ?? id ?? '');
  const {
    postAsync: startAsync,
    isFetching: isStarting,
    error: startError
  } = useStartApplication(data?.name ?? id ?? '');
  const { postAsync: stopAsync, isFetching: isStopping, error: stopError } = useStopApplication(data?.name ?? id ?? '');

  const { subscribe } = useNotifications();

  useEffect(() => {
    return subscribe((message) => {
      if (message.target === 'application' && message.name === id) {
        fetchAsync('').then((result) => {
          setApp(result.data);
        });
      }
    });
  }, [id]);

  const appStatus = useMemo(() => {
    let executionStatus: DeploymentExecutionStatus = 'stopped';
    let provisionStatus: DeploymentProvisionStatus = 'pending';
    app?.deployments?.forEach((deployment) => {
      switch (deployment.provisionStatus) {
        case 'pending':
          break;
        case 'provisioning':
          if (provisionStatus == 'pending') {
            provisionStatus = 'provisioning';
          }
          break;
        case 'provisioned':
          if (provisionStatus !== 'failed') {
            provisionStatus = 'provisioned';
          }
          break;
        case 'failed':
          provisionStatus = 'failed';
          break;
      }
      switch (deployment.executionStatus) {
        case 'stopped':
          break;
        case 'starting':
          if (executionStatus == 'stopped') {
            executionStatus = 'starting';
          }
          break;
        case 'running':
          executionStatus = 'running';
          break;
        case 'stopping':
          if (executionStatus != 'running') {
            executionStatus = 'stopping';
          }
          break;
      }
    });
    return getApplicationStatus(provisionStatus, executionStatus);
  }, [app, app?.deployments]);

  const deployedAt = useMemo(() => {
    let latest = '';
    app?.deployments?.forEach((deployment) => {
      if (!latest || deployment.deployedAt > latest) {
        latest = deployment.deployedAt;
      }
    });
    return latest;
  }, [app, app?.deployments]);

  const conditions = useMemo(() => {
    let allConditions: any[] = [];
    app?.deployments?.forEach((deployment) => {
      if (deployment.deploymentStatus?.conditions) {
        allConditions = allConditions.concat(deployment.deploymentStatus.conditions);
      }
    });
    return allConditions;
  }, [app, app?.deployments]);

  useEffect(() => {
    fetchAsync('').then((result) => {
      setApp(result.data);
    });
  }, [id]);

  const onDeleteApplication = async (applicationName: string) => {
    await deleteAsync(applicationName);
    navigate('/applications');
    reload();
  };

  const onDeployApplication = async (applicationName: string) => {
    await deployAsync(applicationName);
  };

  if (isFetching && !app) {
    return <LoadingSkeleton maxCards={9} />;
  }

  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading applications')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between px-4 py-2 pb-0 max-w-full items-center">
        <h1 className="text-2xl font-bold flex flex-row items-center gap-2">
          {app?.mode == 'workspace' ? <Layers /> : <AppWindow />} <span>{app?.name}</span>{' '}
          {<AppStatus status={appStatus} />}
        </h1>
        <div className="flex flex-row items-center gap-2">
          {app && app?.ports && appStatus !== 'stopped' && (
            <div className="flex flex-row gap-2 mr-4 items-center">
              {app.ports
                .filter((p) => p.protocol !== 'generic')
                .map((port, idx) => (
                  <Button
                    key={`port-${idx}`}
                    size="sm"
                    variant="secondary"
                    onClick={() => {
                      window.open(
                        port.uri && port.uri !== '' ? port.uri : `/share/${app.name}/${port.name}/`,
                        '_blank',
                        'noopener,noreferrer'
                      );
                    }}>
                    {renderIcon(port.icon)} {port.title ?? port.name} <ExternalLink className="size-3" />
                  </Button>
                ))}
            </div>
          )}
          {conditions.length > 0 && (
            <Popover>
              <PopoverTrigger asChild>
                <Button variant="ghost" size="sm">
                  <TriangleAlert fill="oklch(79.5% 0.184 86.047)" className="size-5" />
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-120 flex flex-col gap-2">
                {conditions.map((o, idx) => (
                  <Item variant="outline" key={'condition-' + idx}>
                    <ItemContent>
                      <ItemTitle>{o.reason}</ItemTitle>
                      <ItemDescription>{o.message}</ItemDescription>
                    </ItemContent>
                  </Item>
                ))}
              </PopoverContent>
            </Popover>
          )}
          {(appStatus === 'failed' || appStatus === 'pending' || (app && deployedAt < app.modifiedAt)) && (
            <Button size="sm" disabled={isFetching} onClick={() => onDeployApplication(app?.name ?? '')}>
              {isDeploying ? <Spinner /> : <CloudUpload />} {t('Deploy')}
            </Button>
          )}
          {(appStatus === 'running' || appStatus === 'starting' || appStatus === 'provisioning') && (
            <Button
              onClick={async () => {
                await stopAsync('');
                setApp((prev) => {
                  if (!prev) return prev;
                  return { ...prev, executionStatus: 'stopping' };
                });
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
                await startAsync('');
                setApp((prev) => {
                  if (!prev) return prev;
                  return { ...prev, executionStatus: 'starting' };
                });
              }}
              size="sm"
              disabled={isFetching}
              variant="secondary">
              {isStarting ? <Spinner /> : <Play />} {t('Start')}
            </Button>
          )}
          {(appStatus === 'provisioning' || appStatus === 'starting') && <Spinner />}
          <ApplicationContextMenu
            onDeleteApplication={onDeleteApplication}
            onDeployApplication={onDeployApplication}
            name={app?.name ?? ''}
          />
        </div>
      </div>
      <Separator className="my-2" />
      {app && deployedAt && deployedAt < app.modifiedAt && (
        <div className="p-2 pt-0">
          <Alert className="bg-yellow-50 text-yellow-700 border-yellow-600">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Application has changed')}</AlertTitle>
            <AlertDescription className="text-yellow-700">{t('Your app needs to be redeployed')}</AlertDescription>
          </Alert>
        </div>
      )}
      {deleteError && (
        <div className="p-2 pt-0">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error deleting application')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        </div>
      )}
      {deployError && (
        <div className="p-2 pt-0">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error deploying application')}</AlertTitle>
            <AlertDescription>{deployError}</AlertDescription>
          </Alert>
        </div>
      )}
      {startError && (
        <div className="p-2 pt-0">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error starting application')}</AlertTitle>
            <AlertDescription>{startError}</AlertDescription>
          </Alert>
        </div>
      )}
      {stopError && (
        <div className="p-2 pt-0">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error stopping application')}</AlertTitle>
            <AlertDescription>{stopError}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-4 pt-0 min-h-0 w-full h-full">
        <Tabs defaultValue="events" className="flex flex-col min-h-0 w-full h-full">
          <TabsList>
            <TabsTrigger value="events">
              <div className="flex flex-row items-center gap-2 px-2">
                <Logs /> {t('Events')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="console">
              <div className="flex flex-row items-center gap-2 px-2">
                <Keyboard /> {t('Console')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="monitoring">
              <div className="flex flex-row items-center gap-2 px-2">
                <ChartNoAxesCombined /> {t('Monitoring')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="traces">
              <div className="flex flex-row items-center gap-2 px-2">
                <TextSearch /> {t('Traces')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="variables">
              <div className="flex flex-row items-center gap-2 px-2">
                <Variable /> {t('Environment')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="settings">
              <div className="flex flex-row items-center gap-2 px-2">
                <Cog /> {t('Settings')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="ports">
              <div className="flex flex-row items-center gap-2 px-2">
                <Share2 /> {t('Port sharing')}
              </div>
            </TabsTrigger>
            {app?.mode === 'workspace' && (
              <TabsTrigger value="deployments">
                <div className="flex flex-row items-center gap-2 px-2">
                  <Layers /> {t('Deployments')}
                </div>
              </TabsTrigger>
            )}
            <TabsTrigger value="schedules">
              <div className="flex flex-row items-center gap-2 px-2">
                <CalendarCheck /> {t('Schedules')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="security">
              <div className="flex flex-row items-center gap-2 px-2">
                <UserLock /> {t('Manage Access')}
              </div>
            </TabsTrigger>
          </TabsList>
          <TabsContent value="events" className="flex flex-col min-h-0 w-full h-full">
            <EventsConsole appName={app?.name ?? ''} />
          </TabsContent>
          <TabsContent value="traces" className="flex flex-col min-h-0 w-full h-full">
            <TracesPanel appName={app?.name ?? ''} />
          </TabsContent>
          <TabsContent value="console" className="flex flex-col min-h-0 w-full h-full">
            {app && <ConsolePanel app={app} />}
          </TabsContent>
          <TabsContent value="monitoring" className="flex flex-col min-h-0 w-full h-full">
            <MonitorPanel appName={app?.name ?? ''} maxMemory={app?.size?.memory} maxCpu={app?.size?.cpu} />
          </TabsContent>
          <TabsContent value="variables" className="flex flex-col min-h-0 w-full h-full">
            <EnvironmentVariablesPanel appName={app?.name ?? ''} appVariables={app?.variables} />
          </TabsContent>
          <TabsContent value="settings" className="flex flex-col min-h-0 w-full h-full">
            {app && <SettingsPanel appName={app?.name ?? ''} app={app} />}
          </TabsContent>
          <TabsContent value="ports" className="flex flex-col min-h-0 w-full h-full">
            <PortsPanel application={app!} />
          </TabsContent>
          <TabsContent value="security" className="flex flex-col min-h-0 w-full h-full">
            <AccessPanel application={app!} />
          </TabsContent>
          <TabsContent value="deployments" className="flex flex-col min-h-0 w-full h-full">
            <DeploymentsPanel application={app!} />
          </TabsContent>
          <TabsContent value="schedules" className="flex flex-col min-h-0 w-full h-full">
            <SchedulesPanel appName={app?.name ?? ''} />
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
};

export default Application;
