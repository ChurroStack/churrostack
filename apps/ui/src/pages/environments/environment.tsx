import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Separator } from '@/components/ui/separator';
import { formatDateTime } from '@/extensions';
import {
  useRotateEnvironmentKeys,
  useGetEnvironment,
  useEnvironmentTest,
  useDeleteEnvironment,
  useAnalyzeEnvironmentUsage,
  useGetEnvironmentTotals
} from '@/hooks/data/environments';
import { useMyPermission } from '@/hooks/data/identities';
import {
  AlertCircle,
  AppWindow,
  CheckCircle2Icon,
  CircleAlert,
  Cog,
  FileDown,
  Gauge,
  HardDriveUpload,
  KeyRound,
  Plus,
  ServerCog,
  TextSearch,
  UserLock
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import { NewApplicationDialog } from '../applications/dialogs/new-application';
import { Spinner } from '@/components/ui/spinner';
import { z } from 'zod';
import { Form } from '@/components/ui/form';
import { useForm } from 'react-hook-form';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import InputWithCopy from '@/components/input-with-copy';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { CodeBlock } from '@/components/code-block';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useNotifications } from '@/services/notification-service';
import EnvironmentContextMenu from './menus/environment-menu';
import { useEnvironmentService } from '@/services/environment-services';
import EnvironmentsApplicationsPanel from './panels/applications-panel';
import EnvironmentUsagePanel from './panels/usage-panel';
import { EnvironmentTotalsBar } from './environment-totals-bar';
import AccessPanel from './panels/members-panel';
import { toast } from 'sonner';

const formSchema = z.object({});

const Environment = () => {
  const { t } = useTranslation();
  const { id } = useParams();
  const [showNewApplicationDialog, setShowNewApplicationDialog] = useState(false);
  const { fetchAsync, data, isFetching, error } = useGetEnvironment(id);
  const {
    error: testError,
    isSuccess: isTestSuccess,
    postAsync: testAsync,
    isFetching: isTesting
  } = useEnvironmentTest(id);
  const [environment, setEnvironment] = useState(data);
  const {
    postAsync: rotateEnvironmentKeysAsync,
    isFetching: isRotatingEnvironmentKeys,
    error: rotateEnvironmentKeysError,
    data: rotatedEnvironmentKeysData,
    reset: resetRotateEnvironmentKeys
  } = useRotateEnvironmentKeys(environment?.name ?? data?.name);
  const [defaultView, setDefaultView] = useState<'applications' | 'setup' | 'usage' | 'security'>('applications');
  const [usageReloadSignal, setUsageReloadSignal] = useState(0);
  const navigate = useNavigate();
  const { reload } = useEnvironmentService();
  const { error: deleteError, deleteAsync } = useDeleteEnvironment();
  const { canManage } = useMyPermission(environment?.members);
  const { postAsync: analyzeUsageAsync } = useAnalyzeEnvironmentUsage(environment?.name ?? id ?? '');
  const { data: totals, fetchAsync: fetchTotalsAsync } = useGetEnvironmentTotals(environment?.name ?? id);
  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {},
    mode: 'onChange'
  });

  const { subscribe } = useNotifications();

  useEffect(() => {
    resetRotateEnvironmentKeys();
    fetchAsync('').then((response) => {
      setEnvironment(response?.data);
      if (response?.data?.provisionStatus === 'pending') {
        setDefaultView('setup');
      } else {
        setDefaultView('applications');
      }
    });

    return subscribe((message) => {
      if (message.target === 'environment' && message.name === id) {
        fetchAsync('').then((result) => {
          setEnvironment(result.data);
        });
      }
    });
  }, [id]);

  useEffect(() => {
    if (environment?.name && defaultView === 'setup' && environment?.provisionStatus === 'pending') {
      rotateEnvironmentKeysAsync({});
    }
  }, [environment, defaultView, environment?.provisionStatus]);

  // Refresh the header usage bars when the environment loads and then every 60s
  // (matches the cache cadence ScrapeMetricsJob writes into Redis).
  useEffect(() => {
    if (!environment?.name || environment.provisionStatus !== 'provisioned') return;
    fetchTotalsAsync('');
    const handle = window.setInterval(() => fetchTotalsAsync(''), 60_000);
    return () => window.clearInterval(handle);
  }, [environment?.name, environment?.provisionStatus, fetchTotalsAsync]);

  const testEnvironment = async () => {
    await testAsync({});
  };

  const onDeleteEnvironment = async (environmentName: string) => {
    await deleteAsync(environmentName);
    navigate('/environments');
    reload();
  };

  const onAnalyzeUsage = async () => {
    const result = await analyzeUsageAsync();
    if (result.error) {
      toast.error(result.error);
      return;
    }
    toast.success(t('Usage analysis completed.'));
    setUsageReloadSignal((signal) => signal + 1);
    setDefaultView('usage');
  };

  const downloadValuesYaml = () => {
    if (!rotatedEnvironmentKeysData?.valuesYaml || !rotatedEnvironmentKeysData?.namespace) {
      return;
    }
    const blob = new Blob([rotatedEnvironmentKeysData.valuesYaml], { type: 'text/plain' });

    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = `values.${rotatedEnvironmentKeysData.namespace}.yaml`;

    // Required for Firefox
    document.body.appendChild(a);
    a.click();

    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  if (isFetching || !environment) {
    return <LoadingSkeleton maxCards={9} />;
  }

  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading environments')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between px-4 py-2 pb-0 max-w-full items-center">
        <h1 className="text-2xl font-bold flex flex-row items-center gap-2">
          <ServerCog /> {environment?.name} <Badge variant="outline">{environment?.provisionStatus}</Badge>
        </h1>
        <div className="flex flex-row items-center gap-2">
          <Button
            size="sm"
            variant={environment?.provisionStatus === 'provisioned' ? 'secondary' : 'default'}
            onClick={testEnvironment}
            disabled={isTesting}>
            {isTesting ? <Spinner /> : environment?.health?.healthy ? <CheckCircle2Icon /> : <CircleAlert />}{' '}
            {t('Connect & Sync')}
          </Button>
          <EnvironmentContextMenu
            onDeleteEnvironment={onDeleteEnvironment}
            onAnalyzeUsage={onAnalyzeUsage}
            canManage={canManage}
            name={environment?.name ?? ''}
          />
        </div>
      </div>
      <NewApplicationDialog
        environmentName={environment?.name || ''}
        showDialog={showNewApplicationDialog}
        onClose={() => setShowNewApplicationDialog(false)}
        reload={() => {}}
      />
      <Separator className="my-2" />
      {!isTestSuccess && environment && environment.health && !environment.health.healthy && (
        <div className="flex flex-col px-4">
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Environment cannot be contacted')}</AlertTitle>
            <AlertDescription>{environment.health.error}</AlertDescription>
          </Alert>
        </div>
      )}
      {testError && (
        <div className="flex flex-col px-4">
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error updating environment')}</AlertTitle>
            <AlertDescription>{testError}</AlertDescription>
          </Alert>
        </div>
      )}
      {deleteError && (
        <div className="flex flex-col px-4">
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error deleting environment')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        </div>
      )}
      {isTestSuccess && (
        <div className="flex flex-col mb-4 px-4">
          <Alert className="text-green-800 bg-green-50">
            <CheckCircle2Icon className="size-4" />
            <AlertTitle>{t('Connection successful')}</AlertTitle>
            <AlertDescription>{t('The environment has been contacted successfully.')}</AlertDescription>
          </Alert>
        </div>
      )}

      <div className="flex flex-col gap-4 p-4 pt-0 min-h-0 w-full h-full">
        <Tabs
          value={defaultView}
          onValueChange={(e) => setDefaultView(e as any)}
          className="flex flex-col min-h-0 w-full h-full">
          <div className="w-full flex flex-row justify-between">
            <TabsList>
              <TabsTrigger value="applications">
                <div className="flex flex-row items-center gap-2 px-2">
                  <AppWindow /> {t('Applications')}
                </div>
              </TabsTrigger>
              <TabsTrigger value="setup">
                <div className="flex flex-row items-center gap-2 px-2">
                  <Cog /> {t('Setup')}
                </div>
              </TabsTrigger>
              {canManage && (
                <TabsTrigger value="usage">
                  <div className="flex flex-row items-center gap-2 px-2">
                    <Gauge /> {t('Usage')}
                  </div>
                </TabsTrigger>
              )}
              <TabsTrigger value="security">
                <div className="flex flex-row items-center gap-2 px-2">
                  <UserLock /> {t('Manage Access')}
                </div>
              </TabsTrigger>
            </TabsList>
            {environment.provisionStatus === 'provisioned' && (
              <div className="flex flex-row gap-4 items-center">
                {(environment.definition?.limits?.cpu || environment.definition?.limits?.memory) && (
                  <EnvironmentTotalsBar
                    cpu={totals?.cpu}
                    memory={totals?.memory}
                    cpuQuotaDisplay={environment.definition?.limits?.cpu ?? '∞'}
                    memoryQuotaDisplay={environment.definition?.limits?.memory ?? '∞'}
                  />
                )}
                <Button
                  className="ml-4"
                  variant="default"
                  size="sm"
                  onClick={() => {
                    setShowNewApplicationDialog(true);
                  }}>
                  <Plus /> {t('New application')}
                </Button>
              </div>
            )}
          </div>
          <TabsContent value="applications" className="flex flex-col min-h-0 w-full h-full">
            {environment?.name && <EnvironmentsApplicationsPanel environmentName={environment.name} />}
          </TabsContent>
          {canManage && (
            <TabsContent value="usage" className="flex flex-col min-h-0 w-full h-full">
              {environment?.name && (
                <EnvironmentUsagePanel environmentName={environment.name} reloadSignal={usageReloadSignal} />
              )}
            </TabsContent>
          )}
          <TabsContent value="setup" className="flex flex-col min-h-0 w-full h-full">
            <Card className="w-full max-w-full">
              <CardHeader>
                <CardTitle>{t('Environment setup')}</CardTitle>
                <CardDescription>
                  {t(
                    'Use the following data in your runner configuration and once you finish click the Connect button'
                  )}
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                {rotateEnvironmentKeysError && (
                  <Alert className="mb-4" variant="destructive">
                    <AlertCircle className="size-4" />
                    <AlertTitle>{t('Error generating SSH keys')}</AlertTitle>
                    <AlertDescription>{rotateEnvironmentKeysError}</AlertDescription>
                  </Alert>
                )}
                {!rotatedEnvironmentKeysData && (
                  <ConfirmDialog
                    title={t('Obtain environment configuration')}
                    description={t(
                      'This action will rotate existing encryption keys if any exist. Are you sure you want to continue? This operation cannot be undone.'
                    )}
                    acceptText={t('Rotate & get configuration')}
                    acceptVariant="destructive"
                    onAccept={() => {
                      rotateEnvironmentKeysAsync({});
                    }}>
                    <Button variant="outline" className="mt-4">
                      {isRotatingEnvironmentKeys ? <Spinner /> : <KeyRound />}{' '}
                      {t('Obtain environment configuration (Will rotate SSH keys if any exist)')}
                    </Button>
                  </ConfirmDialog>
                )}
                {rotatedEnvironmentKeysData && (
                  <Accordion type="single" collapsible className="w-full" defaultValue="k8s">
                    <AccordionItem value="data">
                      <AccordionTrigger>
                        <div className="flex flex-row gap-2 items-center">
                          <TextSearch /> {t('Generic configuration information')}
                        </div>
                      </AccordionTrigger>
                      <AccordionContent className="flex flex-col gap-4 text-balance">
                        <FieldGroup className="flex flex-col gap-2">
                          <Field>
                            <FieldLabel>{t('SSH Public Key')}</FieldLabel>
                            <InputWithCopy readOnly value={rotatedEnvironmentKeysData?.sshPublicKey} />
                          </Field>
                          <Field>
                            <FieldLabel>{t('SSH Private Key')}</FieldLabel>
                            <Textarea readOnly value={rotatedEnvironmentKeysData?.sshPrivateKey} />
                          </Field>
                          <Field>
                            <FieldLabel>{t('Encryption Key')}</FieldLabel>
                            <InputWithCopy readOnly value={rotatedEnvironmentKeysData?.encryptionKey} />
                          </Field>
                          <Field>
                            <FieldLabel>{t('Tunnel port')}</FieldLabel>
                            <InputWithCopy readOnly value={`${rotatedEnvironmentKeysData?.port}`} />
                          </Field>
                          <Field>
                            <FieldLabel>{t('Host')}</FieldLabel>
                            <InputWithCopy readOnly value={rotatedEnvironmentKeysData?.host ?? ''} />
                          </Field>
                          <Field>
                            <FieldLabel>{t('Host fingerprint')}</FieldLabel>
                            <InputWithCopy readOnly value={`${rotatedEnvironmentKeysData?.knownHosts}`} />
                          </Field>
                        </FieldGroup>
                      </AccordionContent>
                    </AccordionItem>
                    <AccordionItem value="k8s">
                      <AccordionTrigger>
                        <div className="flex flex-row gap-2 items-center">
                          <HardDriveUpload /> {t('Installation instructions for Kubernetes')}
                        </div>
                      </AccordionTrigger>
                      <AccordionContent className="flex flex-col gap-2 text-balance">
                        <div>
                          To deploy this runner, you must have Helm installed and a Kubernetes cluster properly
                          configured. If you need assistance setting up Kubernetes on a bare-metal server, refer to the
                          following guide:{' '}
                          <a target="_blank" href="https://canonical.com/microk8s">
                            https://canonical.com/microk8s
                          </a>
                        </div>
                        <div className="flex flex-col gap-2">
                          <div>1. Add the ChurroStack Helm repository:</div>
                          <CodeBlock language="bash">
                            helm repo add churrostack https://churrostack.github.io/helm-charts {'\n'}
                            helm repo update
                          </CodeBlock>
                        </div>
                        <div className="flex flex-col gap-2">
                          <div className="flex flex-row gap-2">
                            2. Copy this preconfigured{' '}
                            <a
                              href="#"
                              className="flex flex-row items-center gap-2 underline"
                              onClick={(e) => {
                                e.preventDefault();
                                e.stopPropagation();
                                downloadValuesYaml();
                              }}>
                              values.{rotatedEnvironmentKeysData?.namespace}.yaml <FileDown className="size-4" />
                            </a>{' '}
                            file to your local machine so it can be applied using Helm.
                          </div>
                          <CodeBlock language="yaml">{rotatedEnvironmentKeysData?.valuesYaml}</CodeBlock>
                        </div>
                        <div className="flex flex-col gap-2">
                          <div>
                            3. Install the Helm chart in your preferred namespace (defaults to{' '}
                            {rotatedEnvironmentKeysData?.namespace}). If you plan to deploy multiple environments,
                            ensure that each one uses a distinct namespace.
                          </div>
                          <CodeBlock language="bash">
                            helm upgrade --install churrun churrostack/churrun-kubernetes -f values.
                            {rotatedEnvironmentKeysData?.namespace}.yaml -n {rotatedEnvironmentKeysData?.namespace}{' '}
                            --create-namespace
                          </CodeBlock>
                        </div>
                        <div className="flex flex-col gap-2">
                          4. Once you have applied the Helm chart, return here and click the "Connect & Sync" button to
                          finalize the setup.
                        </div>
                      </AccordionContent>
                    </AccordionItem>
                  </Accordion>
                )}

                <Form {...form}>
                  <form>
                    <div className="flex flex-col gap-4 p-4 pt-0">
                      <FieldGroup className="flex flex-row gap-2">
                        <Field>
                          <FieldLabel>{t('Created at')}</FieldLabel>
                          <Input readOnly value={formatDateTime(environment?.createdAt ?? '')} />
                        </Field>
                        <Field>
                          <FieldLabel>{t('Created by')}</FieldLabel>
                          <Input readOnly value={environment?.createdBy?.displayName ?? environment?.createdBy?.name} />
                        </Field>
                        <Field>
                          <FieldLabel>{t('Modified at')}</FieldLabel>
                          <Input readOnly value={formatDateTime(environment?.modifiedAt ?? '')} />
                        </Field>
                        <Field>
                          <FieldLabel>{t('Modified by')}</FieldLabel>
                          <Input
                            readOnly
                            value={environment?.modifiedBy?.displayName ?? environment?.modifiedBy?.name}
                          />
                        </Field>
                      </FieldGroup>
                    </div>
                  </form>
                </Form>
              </CardContent>
              <CardFooter className="justify-end">
                <Button onClick={testEnvironment} disabled={isTesting}>
                  {isTesting ? <Spinner /> : <CheckCircle2Icon />} {t('Connect & Sync')}
                </Button>
              </CardFooter>
            </Card>
          </TabsContent>
          <TabsContent value="security" className="flex flex-col min-h-0 w-full h-full">
            <AccessPanel environment={environment!} />
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
};

export default Environment;
