import { cn } from '@/lib/utils';
import { Plus, Trash2 } from 'lucide-react';
import { Controller, useFieldArray, type Control } from 'react-hook-form';
import z from 'zod';
import { Button } from './ui/button';
import { useTranslation } from 'react-i18next';
import { PermissionHelper } from '@/hooks/data/identities';
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from './ui/select';
import IdentityPicker from '@/pickers/identity-picker';

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

type MemberEditorFieldProps = {
  control: Control<HasMembers>;
  className?: string;
};

const MembersEditor = ({ control, className }: MemberEditorFieldProps) => {
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
                <IdentityPicker className="flex-1" {...identityNameField} type={field.type} />
              )}
            />
            <Controller
              name={`members.${index}.permission`}
              control={control}
              render={({ field: permissionField }) => (
                <Select
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
            disabled={fields.length === 1}>
            <Trash2 />
          </Button>
        </div>
      ))}
      <Button
        variant="ghost"
        onClick={() => {
          append({ identityName: '', permission: PermissionHelper.Execute });
        }}>
        <Plus /> {t('Add member')}
      </Button>
    </div>
  );
};

export default MembersEditor;
