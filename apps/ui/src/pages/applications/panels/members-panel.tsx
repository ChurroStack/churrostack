import MembersEditor, { useMembersForm } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useUpdateApplication, type ApplicationItem } from '@/hooks/data/applications';
import { AlertCircle, Save } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

const AccessPanel = ({
  application,
  canManage,
  onUpdated
}: {
  application: ApplicationItem;
  canManage: boolean;
  onUpdated?: (application: ApplicationItem) => void;
}) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateApplication(application.name ?? '');

  const { form, submit, isSubmitting } = useMembersForm<ApplicationItem>({
    members: application.members,
    onSave: (members) => patchAsync({ members }),
    onSaved: (updated) => {
      onUpdated?.(updated);
      toast.success(t('Application members have been updated successfully.'));
    }
  });

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage who can access this application')}</h3>
        </div>
        <div className="flex flex-row items-center">
          <Button variant="default" size="sm" disabled={!canManage || isFetching || isSubmitting} onClick={submit}>
            {isFetching || isSubmitting ? <Spinner /> : <Save />} {t('Save Changes')}
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
        <MembersEditor control={form.control} disabled={!canManage} />
      </div>
    </div>
  );
};

export default AccessPanel;
