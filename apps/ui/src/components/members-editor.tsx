import { cn } from '@/lib/utils';
import { Plus, Trash2 } from 'lucide-react';
import { useEffect } from 'react';
import { Controller, useFieldArray, useForm, type Control } from 'react-hook-form';
import z from 'zod';
import { Button } from './ui/button';
import { useTranslation } from 'react-i18next';
import { PermissionHelper, type MemberSummary } from '@/hooks/data/identities';
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from './ui/select';
import IdentityPicker from '@/pickers/identity-picker';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import type { Response } from '@/hooks/data/core';
import { toast } from 'sonner';

export const MemberSchema = z.object({
  identityName: z.string().min(1, 'Enter the identity name'),
  displayName: z.string().optional(),
  type: z.string().optional(),
  permission: z.number()
});
export type Member = z.infer<typeof MemberSchema>;

export type HasMembers = {
  members: Member[];
};

const formSchema = z.object({
  members: z.array(MemberSchema).min(1, 'At least one member is required')
});

const toFormMembers = (members?: MemberSummary[] | null): Member[] =>
  (members ?? []).map((member) => ({
    identityName: member?.identity?.name,
    type: member?.identity?.type,
    permission: member?.permission
  })) as Member[];

/**
 * Owns the member-editing form contract shared by the environment, application and LLM
 * Manage Access panels: reseeding from server data without clobbering unsaved edits, running
 * validation on save, and reseeding again from the server's authoritative response.
 */
export function useMembersForm<TEntity>({
  members,
  onSave,
  onSaved
}: {
  members?: MemberSummary[] | null;
  onSave: (members: Member[]) => Promise<Response<TEntity>>;
  onSaved?: (updated: TEntity) => void;
}) {
  const { t } = useTranslation();
  const form = useForm<HasMembers>({
    resolver: standardSchemaResolver(formSchema),
    mode: 'onChange'
  });
  // RHF's formState is a lazily-subscribed proxy: a property must be read during render to stay
  // live. Reading it only inside the effect below would silently freeze at its initial value.
  const { isDirty, isSubmitting } = form.formState;

  useEffect(() => {
    if (isDirty) return;
    form.reset({ members: toFormMembers(members) });
  }, [members, form, isDirty]);

  const submit = form.handleSubmit(
    async (values) => {
      const response = await onSave(values.members);
      if (response.data) {
        form.reset({ members: values.members });
        onSaved?.(response.data);
      }
      return response;
    },
    () => {
      toast.error(t('Fix the highlighted member fields before saving.'));
    }
  );

  return { form, submit, isSubmitting };
}

type MemberEditorFieldProps = {
  control: Control<HasMembers>;
  className?: string;
  disabled?: boolean;
};

const MembersEditor = ({ control, className, disabled }: MemberEditorFieldProps) => {
  const { t } = useTranslation();
  const { fields, append, remove } = useFieldArray({
    control: control,
    name: 'members'
  });

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      {fields.map((field, index) => (
        <div key={field.id} className="flex flex-row gap-2 w-full">
          <div className="flex flex-row gap-2 justify-evenly w-full">
            <Controller
              name={`members.${index}.identityName`}
              control={control}
              render={({ field: identityNameField }) => (
                <IdentityPicker
                  className="flex-1"
                  {...identityNameField}
                  type={field.type}
                  readonly={disabled}
                />
              )}
            />
            <Controller
              name={`members.${index}.permission`}
              control={control}
              render={({ field: permissionField }) => (
                <Select
                  disabled={disabled}
                  value={`${permissionField.value}`}
                  onValueChange={(value) => permissionField.onChange(Number(value))}>
                  <SelectTrigger className="flex-1">
                    <SelectValue placeholder={t('Select the role type')} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectGroup>
                      <SelectLabel>{t('Role')}</SelectLabel>
                      <SelectItem value={`${PermissionHelper.Execute}`}>{t('User')}</SelectItem>
                      <SelectItem value={`${PermissionHelper.Execute | PermissionHelper.Read}`}>
                        {t('Reader')}
                      </SelectItem>
                      <SelectItem
                        value={`${PermissionHelper.Execute | PermissionHelper.Read | PermissionHelper.Write}`}>
                        {t('Collaborator')}
                      </SelectItem>
                      <SelectItem
                        value={`${PermissionHelper.Execute | PermissionHelper.Read | PermissionHelper.Write | PermissionHelper.Manage}`}>
                        {t('Manager')}
                      </SelectItem>
                    </SelectGroup>
                  </SelectContent>
                </Select>
              )}
            />
          </div>
          <Button
            variant="ghost"
            onClick={(e) => {
              e.stopPropagation();
              e.preventDefault();
              remove(index);
            }}
            disabled={disabled || fields.length === 1}>
            <Trash2 />
          </Button>
        </div>
      ))}
      <Button
        variant="ghost"
        disabled={disabled}
        onClick={() => {
          append({ identityName: '', permission: PermissionHelper.Execute });
        }}>
        <Plus /> {t('Add member')}
      </Button>
    </div>
  );
};

export default MembersEditor;
