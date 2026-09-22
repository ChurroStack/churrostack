import MembersEditor, { useMembersForm } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useUpdateLlm, type LlmItem } from '@/hooks/data/llms';
import { AlertCircle, Save } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

const AccessPanel = ({
  llm,
  canManage,
  onUpdated
}: {
  llm: LlmItem;
  canManage: boolean;
  onUpdated?: (llm: LlmItem) => void;
}) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateLlm(llm.id ?? '');

  const { form, submit, isSubmitting } = useMembersForm<LlmItem>({
    members: llm.members,
    onSave: (members) => patchAsync({ members }),
    onSaved: (updated) => {
      onUpdated?.(updated);
      toast.success(t('LLM members have been updated successfully.'));
    }
  });

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage who can access this LLM')}</h3>
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
            <AlertTitle>{t('Error updating LLM members')}</AlertTitle>
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
