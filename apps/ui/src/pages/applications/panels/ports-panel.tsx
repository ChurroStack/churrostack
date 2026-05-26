import { MemberSchema } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Item, ItemActions, ItemContent, ItemDescription, ItemMedia, ItemTitle } from '@/components/ui/item';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue
} from '@/components/ui/select';
import { Spinner } from '@/components/ui/spinner';
import { renderIcon } from '@/extensions';
import { useUpdateApplication, type ApplicationItem, type PortDefinition } from '@/hooks/data/applications';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { AlertCircle, CircleSlash2, GlobeLock, Save, ShieldOff, ShieldUser } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import z from 'zod';
import EditPortDialog from '../dialogs/edit-port';

const portsSchema = z.object({
  name: z.string(),
  description: z.string().optional(),
  icon: z.string().optional(),
  title: z.string().optional(),
  port: z.number().min(1).max(65535),
  sharing: z.enum(['none', 'members']).default('none'),
  authentication: z.enum(['anonymous', 'jwt', 'jwt_dcr', 'oidc']).default('oidc'),
  members: z.array(MemberSchema).optional()
});

const formSchema = z.object({
  ports: z.array(portsSchema)
});

const PortTitle = ({ appName, ports, portName }: { appName: string; ports: PortDefinition[]; portName: string }) => {
  const [showPortDialog, setShowPortDialog] = useState(false);
  const port = ports.find((p) => p.name === portName);
  return (
    <>
      <ItemTitle onClick={() => setShowPortDialog(true)} className="cursor-pointer">
        {port?.title ?? port?.name}
      </ItemTitle>
      <EditPortDialog
        showDialog={showPortDialog}
        appName={appName}
        ports={ports}
        portName={portName}
        onClose={() => setShowPortDialog(false)}
      />
    </>
  );
};

const PortsPanel = ({ application }: { application: ApplicationItem }) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateApplication(application.name ?? '');

  const form = useForm<z.input<typeof formSchema>, unknown, z.output<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      ports: []
    },
    mode: 'onChange'
  });

  const ports = form.watch('ports');

  useEffect(() => {
    form.reset({
      ports: application?.ports?.map((port) => ({
        name: port?.name,
        icon: port?.icon,
        title: port?.title,
        description: port?.description,
        port: port?.port,
        authentication: port?.authentication ?? 'oidc',
        sharing: port?.sharing ?? 'none',
        members: port?.members
      })) ?? ['']
    });
  }, [application]);

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage port sharing configuration')}</h3>
        </div>
        <div className="flex flex-row items-center">
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              patchAsync({
                ports
              }).then((response) => {
                if (!response.error) {
                  toast.success('Application ports have been updated successfully.');
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
            <AlertTitle>{t('Error updating application ports')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        {ports.map((port, index) => (
          <Item variant="outline">
            <ItemMedia key={`port-${index}`}>{renderIcon(port.icon ?? 'default-port-icon', 'size-6')}</ItemMedia>
            <ItemContent>
              <PortTitle
                appName={application.name ?? ''}
                ports={ports.map(
                  (port) =>
                    ({
                      name: port.name,
                      title: port.title,
                      port: port.port,
                      description: port.description,
                      sharing: port.sharing,
                      authentication: port.authentication,
                      members: port.members,
                      icon: port.icon
                    }) as PortDefinition
                )}
                portName={port.name}
              />
              <ItemDescription>{port.description}</ItemDescription>
            </ItemContent>
            <ItemActions>
              <div className="flex flex-row gap-2 items-center">
                <div className="flex flex-col gap-2">
                  <Label>{t('Sharing mode')}</Label>
                  <Select
                    value={port.sharing}
                    onValueChange={(value) => form.setValue(`ports.${index}.sharing`, value as any)}>
                    <SelectTrigger className="flex-1 py-1 data-[size=default]:h-8">
                      <SelectValue placeholder={t('Sharing mode')} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectGroup>
                        <SelectLabel>{t('Sharing mode')}</SelectLabel>
                        <SelectItem value={`none`}>
                          {' '}
                          <CircleSlash2 /> {t('Collaborators only')}
                        </SelectItem>
                        <SelectItem value={`members`}>
                          {' '}
                          <GlobeLock />
                          {t('All members with access to the application')}
                        </SelectItem>
                      </SelectGroup>
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex flex-col gap-2">
                  <Label>{t('Authentication')}</Label>
                  <Select
                    value={port.authentication}
                    onValueChange={(value) => form.setValue(`ports.${index}.authentication`, value as any)}>
                    <SelectTrigger className="flex-1 py-1 data-[size=default]:h-8">
                      <SelectValue placeholder={t('Authorization method')} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectGroup>
                        <SelectLabel>{t('Authorization method')}</SelectLabel>
                        <SelectItem value={`oidc`}>
                          {' '}
                          <ShieldUser /> {t('Open ID Connect (for Web apps)')}
                        </SelectItem>
                        <SelectItem value={`jwt`}>
                          {' '}
                          <ShieldUser />
                          {t('OAuth (for APIs)')}
                        </SelectItem>
                        <SelectItem value={`jwt_dcr`}>
                          <ShieldUser /> {t('OAuth with Dynamic Client Registration (For MCP tools)')}
                        </SelectItem>
                        <SelectItem value={`anonymous`}>
                          <ShieldOff /> {t('Anonymous (WARNING! everyone could access)')}
                        </SelectItem>
                      </SelectGroup>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </ItemActions>
          </Item>
        ))}
      </div>
    </div>
  );
};

export default PortsPanel;
