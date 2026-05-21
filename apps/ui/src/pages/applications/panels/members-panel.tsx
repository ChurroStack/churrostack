import MembersEditor, { MemberSchema } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useUpdateApplication, type ApplicationItem } from '@/hooks/data/applications';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { AlertCircle, Save } from 'lucide-react';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import z from 'zod';

const formSchema = z.object({
  members: z.array(MemberSchema).min(1, 'At least one member is required')
});

const AccessPanel = ({ application }: { application: ApplicationItem }) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateApplication(application.name ?? '');

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    mode: 'onChange'
  });

  useEffect(() => {
    form.reset({
      members: application.members?.map((member) => ({
        identityName: member?.identity?.name,
        type: member?.identity?.type,
        permission: member?.permission
      })) ?? ['']
    });
  }, [application.members, form]);

  const members = form.watch('members');

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage who can access this application')}</h3>
        </div>
        <div className="flex flex-row items-center">
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              patchAsync({
                members
              }).then((response) => {
                if (!response.error) {
                  toast.success('Application members have been updated successfully.');
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
            <AlertTitle>{t('Error updating application members')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        <MembersEditor control={form.control} />
      </div>
    </div>
  );
};

export default AccessPanel;
