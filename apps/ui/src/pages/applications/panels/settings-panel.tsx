import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { formatDateTime, renderIcon } from '@/extensions';
import { useUpdateApplication, type ApplicationExtensionItem, type ApplicationItem } from '@/hooks/data/applications';
import { AlertCircle, Save } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import EnvironmentSizePicker from '@/pickers/size-picker';
import { useGetTemplate, type TemplateParameterDefinition } from '@/hooks/data/templates';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import TagChipsInput from '@/components/tag-chips-input';
import StorageExtensionTable from './storage-extension-table';

const STORAGE_EXTENSION_TEMPLATE = 'com.churrostack.extension.storage';

const ExtensionHandler = ({
  app,
  extensionName,
  extensionTemplate,
  environmentType,
  onExtensionChange
}: {
  app?: ApplicationItem;
  extensionName: string;
  extensionTemplate: string;
  environmentType: string;
  onExtensionChange?: (extension: ApplicationExtensionItem) => void;
}) => {
  const { fetchAsync, data: template } = useGetTemplate(`${extensionTemplate}:${environmentType}`);
  const [extension, setExtension] = useState<ApplicationExtensionItem | undefined>(undefined);

  const setParameters = (parameters: { [name: string]: string[] }) => {
    const updatedExtension = {
      ...extension,
      parameters
    } as ApplicationExtensionItem;
    setExtension(updatedExtension);
    onExtensionChange?.(updatedExtension);
  };

  const setExtensionEnabled = (enabled: boolean) => {
    const updatedExtension = {
      ...extension,
      enabled
    } as ApplicationExtensionItem;
    setExtension(updatedExtension);
    onExtensionChange?.(updatedExtension);
  };

  useEffect(() => {
    fetchAsync('');
  }, [extensionTemplate, environmentType]);

  useEffect(() => {
    setExtension(
      app?.extensions?.find((ext) => ext.name === extensionName) ?? {
        enabled: true,
        name: extensionName,
        template: extensionTemplate,
        parameters: {}
      }
    );
  }, [app, extensionName, extensionTemplate]);

  return (
    <Card className="shadow-none p-4 gap-2">
      <CardHeader className="px-0">
        <div className="flex flex-row items-center gap-4 w-full">
          <Switch
            checked={extension?.enabled === undefined ? true : !!extension?.enabled}
            onCheckedChange={(checked) => setExtensionEnabled(checked)}
          />
          <div className="flex flex-row items-center gap-2 w-full">
            {renderIcon(template?.definition?.icon ?? 'blocks', 'size-4')}
            <span className="uppercase text-sm font-semibold">{extension?.name ?? extensionName}</span>
          </div>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        {template?.definition?.parameters && extension?.enabled && (
          <FieldGroup className="flex flex-col gap-4">
            {Object.entries(template.definition.parameters).map(([key, param]) => (
              <Field key={`param-${key}`}>
                <FieldLabel>{param.title}</FieldLabel>
                <ParameterInput
                  parameterName={key}
                  parameterDefinition={param}
                  onChange={(value) => setParameters({ ...extension?.parameters, [key]: value })}
                  parameters={extension?.parameters ?? {}}
                />
              </Field>
            ))}
          </FieldGroup>
        )}
      </CardContent>
    </Card>
  );
};

const ParameterInput = ({
  parameterName,
  onChange,
  parameters,
  parameterDefinition
}: {
  parameterName: string;
  onChange: (value: string[]) => void;
  parameters: { [name: string]: string[] };
  parameterDefinition: TemplateParameterDefinition;
}) => {
  const value =
    parameters && parameters[parameterName] && parameters[parameterName].length > 0
      ? parameters[parameterName][0]
      : parameterDefinition.default_value && parameterDefinition.default_value.length > 0
        ? parameterDefinition.default_value[0]
        : '';
  // 'string' | 'number' | 'boolean' | 'list';
  switch (parameterDefinition.type) {
    case 'string':
    default:
      switch (parameterDefinition.ui_hint) {
        case 'textarea':
          return <Textarea value={value} onChange={(e) => onChange([e.target.value])} />;
        case 'password':
          return <Input type="password" value={value} onChange={(e) => onChange([e.target.value])} />;
        default:
          return <Input value={value} onChange={(e) => onChange([e.target.value])} />;
      }
  }
};

const SettingsPanel = ({
  appName,
  app
}: {
  appName: string;
  app: ApplicationItem;
  onEnvironmentVariableSet?: (name: string, value: string) => void;
}) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateApplication(appName);
  const [size, setSize] = useState(app?.size ?? undefined);
  const [description, setDescription] = useState(app?.metadata?.description ?? '');
  const [parameters, setParameters] = useState<{ [name: string]: string[] }>(app?.parameters ?? {});
  const [extensions, setExtensions] = useState<ApplicationExtensionItem[]>(app?.extensions ?? []);
  const [autoStartEnabled, setAutoStartEnabled] = useState<boolean>(!!app?.metadata?.autoStart?.enabled);
  const [autoStopEnabled, setAutoStopEnabled] = useState<boolean>(!!app?.metadata?.autoStop?.enabled);
  const [autoStopIdleMinutes, setAutoStopIdleMinutes] = useState<number>(app?.metadata?.autoStop?.idleMinutes ?? 60);
  const [tags, setTags] = useState<string[]>(app?.tags ?? []);

  useEffect(() => {
    setSize(app?.size ?? undefined);
    setDescription(app?.metadata?.description ?? '');
    setParameters(app?.parameters ?? {});
    setExtensions(app?.extensions ?? []);
    setAutoStartEnabled(!!app?.metadata?.autoStart?.enabled);
    setAutoStopEnabled(!!app?.metadata?.autoStop?.enabled);
    setAutoStopIdleMinutes(app?.metadata?.autoStop?.idleMinutes ?? 60);
    setTags(app?.tags ?? []);
  }, [app]);

  const setExtension = (extension: ApplicationExtensionItem) => {
    delete (extension as any).template;
    const existingExtension = extensions.find((ext) => ext.name === extension.name);
    if (existingExtension) {
      setExtensions(
        extensions.map((ext) => {
          if (ext.name === extension.name) {
            return extension;
          }
          return ext;
        })
      );
    } else {
      setExtensions([...extensions, extension]);
      return;
    }
  };

  // Storage is multi-instance: the table owns the full set of storage rows and
  // replaces every storage extension (name === baseName or "baseName-N") at once.
  const setStorageExtensions = (baseName: string, items: ApplicationExtensionItem[]) => {
    const isStorage = (name: string) => name === baseName || name.startsWith(`${baseName}-`);
    setExtensions((prev) => [...prev.filter((ext) => !isStorage(ext.name)), ...items]);
  };

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center gap-2 leading-none">
          <h3 className="text-sm text-muted-foreground leading-none">
            {t('Settings for')} {appName}
          </h3>
          {app?.template && (
            <span className="text-xs text-muted-foreground flex flex-row items-center gap-1 leading-none">
              {renderIcon(app.template.icon ?? app.template.definition?.icon ?? 'blocks', 'size-4')}
              <span className="leading-none">
                {app.template.title ?? app.template.name}
                {app.template.title && app.template.name ? ` - ${app.template.name}` : ''}
              </span>
            </span>
          )}
        </div>
        <div className="flex flex-row items-center">
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              patchAsync({
                size,
                parameters,
                extensions,
                tags,
                metadata: {
                  description,
                  autoStart: { enabled: autoStartEnabled },
                  autoStop: { enabled: autoStopEnabled, idleMinutes: autoStopIdleMinutes }
                }
              }).then((response) => {
                if (!response.error) {
                  toast.success('Application settings have been updated successfully.');
                }
              });
            }}>
            {isFetching ? <Spinner /> : <Save />} {t('Save Changes')}
          </Button>
        </div>
      </div>
      {error && (
        <div className="p-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error updating application settings')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        <div className="flex flex-row gap-2">
          <Card className="shadow-none p-4 gap-2 flex-1">
            <CardHeader className="px-0">
              <div className="flex flex-row items-center gap-4 w-full">
                <Switch checked={autoStartEnabled} onCheckedChange={setAutoStartEnabled} />
                <div className="flex flex-col">
                  <span className="uppercase text-sm font-semibold">{t('Auto-start')}</span>
                  <FieldDescription>
                    {t('Start the application automatically when a request arrives.')}
                  </FieldDescription>
                </div>
              </div>
            </CardHeader>
          </Card>
          <Card className="shadow-none p-4 gap-2 flex-1">
            <CardHeader className="px-0">
              <div className="flex flex-row items-center gap-4 w-full">
                <Switch checked={autoStopEnabled} onCheckedChange={setAutoStopEnabled} />
                <div className="flex flex-col flex-1">
                  <span className="uppercase text-sm font-semibold">{t('Auto-stop')}</span>
                  <FieldDescription>
                    {t('Stop the application after a period without requests or CPU activity.')}
                  </FieldDescription>
                </div>
              </div>
            </CardHeader>
            {autoStopEnabled && (
              <CardContent className="p-0">
                <Field>
                  <FieldLabel>{t('Stop when idle for')}</FieldLabel>
                  <Select
                    value={String(autoStopIdleMinutes)}
                    onValueChange={(v) => setAutoStopIdleMinutes(Number(v))}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="30">{t('30 minutes')}</SelectItem>
                      <SelectItem value="60">{t('1 hour')}</SelectItem>
                      <SelectItem value="120">{t('2 hours')}</SelectItem>
                      <SelectItem value="240">{t('4 hours')}</SelectItem>
                      <SelectItem value="480">{t('8 hours')}</SelectItem>
                    </SelectContent>
                  </Select>
                </Field>
              </CardContent>
            )}
          </Card>
        </div>
        <FieldGroup className="flex flex-row gap-2">
          <Field>
            <FieldLabel>{t('Application description')}</FieldLabel>
            <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </FieldGroup>
        <FieldGroup className="flex flex-row gap-2">
          <Field>
            <FieldLabel>{t('Tags')}</FieldLabel>
            <FieldDescription>
              {t('Lowercase letters, digits, hyphens or underscores (max 32 chars per tag).')}
            </FieldDescription>
            <TagChipsInput value={tags} onChange={setTags} />
          </Field>
        </FieldGroup>
        <FieldGroup className="flex flex-row gap-2">
          <Field>
            <FieldLabel>{t('Application size')}</FieldLabel>
            <EnvironmentSizePicker
              value={size}
              onChange={(newSize) => setSize(newSize)}
              environmentName={app.environmentName}
            />
          </Field>
          <Field className="max-w-50">
            <FieldLabel>{t('CPU (millicores)')}</FieldLabel>
            <Input readOnly value={size?.cpu} />
          </Field>
          <Field className="max-w-50">
            <FieldLabel>{t('Memory (Bytes)')}</FieldLabel>
            <Input readOnly value={size?.memory} />
          </Field>
          <Field className="max-w-50">
            <FieldLabel>{t('GPU (Count or MIG partition)')}</FieldLabel>
            <Input readOnly value={size?.gpu} />
          </Field>
          <Field className="max-w-50">
            <FieldLabel>{t('Storage (Bytes)')}</FieldLabel>
            <Input readOnly value={size?.storage} />
          </Field>
        </FieldGroup>
        {app.template.definition.parameters && (
          <FieldGroup className="flex flex-col gap-4">
            {Object.entries(app.template.definition.parameters).map(([key, param]) => (
              <Field key={`param-${key}`}>
                <FieldLabel>{param.title}</FieldLabel>
                {param.description && <FieldDescription>{param.description}</FieldDescription>}
                <ParameterInput
                  parameterName={key}
                  parameterDefinition={param}
                  onChange={(value) => setParameters({ ...parameters, [key]: value })}
                  parameters={parameters}
                />
              </Field>
            ))}
          </FieldGroup>
        )}
        {app.template?.definition?.extensions &&
          app.template.definition.extensions.map((extension, idx) =>
            extension.template === STORAGE_EXTENSION_TEMPLATE ? (
              <StorageExtensionTable
                key={`extension-${idx}`}
                app={app}
                baseName={extension.name}
                extensionTemplate={extension.template}
                environmentType={app.template.definition.target}
                environmentName={app.environmentName}
                onChange={(items) => setStorageExtensions(extension.name, items)}
              />
            ) : (
              <ExtensionHandler
                key={`extension-${idx}`}
                app={app}
                extensionTemplate={extension.template}
                extensionName={extension.name}
                environmentType={app.template.definition.target}
                onExtensionChange={setExtension}
              />
            )
          )}
        <FieldGroup className="flex flex-row gap-2">
          <Field>
            <FieldLabel>{t('Created at')}</FieldLabel>
            <Input readOnly value={formatDateTime(app?.createdAt ?? '')} />
          </Field>
          <Field>
            <FieldLabel>{t('Created by')}</FieldLabel>
            <Input readOnly value={app?.createdBy?.displayName ?? app?.createdBy?.name} />
          </Field>
          <Field>
            <FieldLabel>{t('Modified at')}</FieldLabel>
            <Input readOnly value={formatDateTime(app?.modifiedAt ?? '')} />
          </Field>
          <Field>
            <FieldLabel>{t('Modified by')}</FieldLabel>
            <Input readOnly value={app?.modifiedBy?.displayName ?? app?.modifiedBy?.name} />
          </Field>
        </FieldGroup>
      </div>
    </div>
  );
};

export default SettingsPanel;
