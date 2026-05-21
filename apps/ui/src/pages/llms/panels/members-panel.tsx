import MembersEditor, { MemberSchema } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useUpdateLlm, type LlmItem } from '@/hooks/data/llms';
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

const AccessPanel = ({ llm }: { llm: LlmItem }) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateLlm(llm.id ?? '');

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    mode: 'onChange'
  });

  useEffect(() => {
    form.reset({
      members: llm.members.map((member) => ({
        identityName: member?.identity?.name,
        type: member?.identity?.type,
        permission: member?.permission
      })) ?? ['']
    });
  }, [llm.members, form]);

  const members = form.watch('members');

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage who can access this LLM')}</h3>
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
                  toast.success('LLM members have been updated successfully.');
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
            <AlertTitle>{t('Error updating LLM members')}</AlertTitle>
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
