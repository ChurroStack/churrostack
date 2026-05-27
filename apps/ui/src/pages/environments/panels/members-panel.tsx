import MembersEditor, { MemberSchema } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useUpdateEnvironment, type EnvironmentItem } from '@/hooks/data/environments';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { AlertCircle, Save } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import z from 'zod';
import TagChipsInput from '@/components/tag-chips-input';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field';

const formSchema = z.object({
  members: z.array(MemberSchema).min(1, 'At least one member is required')
});

const AccessPanel = ({ environment }: { environment: EnvironmentItem }) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateEnvironment(environment.name ?? '');
  const [tags, setTags] = useState<string[]>(environment.tags ?? []);

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    mode: 'onChange'
  });

  useEffect(() => {
    form.reset({
      members: environment.members?.map((member) => ({
        identityName: member?.identity?.name,
        type: member?.identity?.type,
        permission: member?.permission
      })) ?? ['']
    });
    setTags(environment.tags ?? []);
  }, [environment.members, environment.tags, form]);

  const members = form.watch('members');

  const tagsChanged = (a: string[], b: string[]) => {
    if (a.length !== b.length) return true;
    const sa = [...a].sort();
    const sb = [...b].sort();
    return sa.some((v, i) => v !== sb[i]);
  };

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage tags and who can access this environment')}</h3>
        </div>
        <div className="flex flex-row items-center">
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              const didTagsChange = tagsChanged(tags, environment.tags ?? []);
              patchAsync({
                members,
                tags
              }).then((response) => {
                if (!response.error) {
                  toast.success(
                    didTagsChange
                      ? t('Environment has been updated successfully.')
                      : t('Environment members have been updated successfully.')
                  );
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
            <AlertTitle>{t('Error updating environment')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        <FieldGroup>
          <Field>
            <FieldLabel>{t('Tags')}</FieldLabel>
            <FieldDescription>
              {t('Lowercase letters, digits, hyphens or underscores (max 32 chars per tag).')}
            </FieldDescription>
            <TagChipsInput value={tags} onChange={setTags} />
          </Field>
        </FieldGroup>
        <MembersEditor control={form.control} />
      </div>
    </div>
  );
};

export default AccessPanel;
