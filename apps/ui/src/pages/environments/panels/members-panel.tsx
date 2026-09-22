import MembersEditor, { useMembersForm } from '@/components/members-editor';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useUpdateEnvironment, type EnvironmentItem } from '@/hooks/data/environments';
import { AlertCircle, Save } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import TagChipsInput from '@/components/tag-chips-input';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field';

const tagsChanged = (a: string[], b: string[]) => {
  if (a.length !== b.length) return true;
  const sa = [...a].sort();
  const sb = [...b].sort();
  return sa.some((v, i) => v !== sb[i]);
};

const AccessPanel = ({
  environment,
  canManage,
  onUpdated
}: {
  environment: EnvironmentItem;
  canManage: boolean;
  onUpdated?: (environment: EnvironmentItem) => void;
}) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateEnvironment(environment.name ?? '');
  const [tags, setTags] = useState<string[]>(environment.tags ?? []);
  const [tagsDirty, setTagsDirty] = useState(false);

  useEffect(() => {
    if (tagsDirty) return;
    setTags(environment.tags ?? []);
  }, [environment.tags, tagsDirty]);

  const { form, submit, isSubmitting } = useMembersForm<EnvironmentItem>({
    members: environment.members,
    onSave: (members) => patchAsync({ members, tags }),
    onSaved: (updated) => {
      setTagsDirty(false);
      onUpdated?.(updated);
      toast.success(
        tagsChanged(tags, environment.tags ?? [])
          ? t('Environment has been updated successfully.')
          : t('Environment members have been updated successfully.')
      );
    }
  });

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage tags and who can access this environment')}</h3>
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
            <TagChipsInput
              value={tags}
              onChange={(next) => {
                setTagsDirty(true);
                setTags(next);
              }}
              disabled={!canManage}
            />
          </Field>
        </FieldGroup>
        <MembersEditor control={form.control} disabled={!canManage} />
      </div>
    </div>
  );
};

export default AccessPanel;
