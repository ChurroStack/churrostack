'use client';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import {
  type IdentityItem,
  type IdentityType,
  type IdentityWithAssignedItem,
  useIdentity,
  useUpsertIdentity
} from '@/hooks/data/identities';
import { useEffect, useState } from 'react';
import { z } from 'zod';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle, AppWindowMac, Contact, ShieldUser, User, Users, X } from 'lucide-react';
import { LoadingProgress } from '@/components/loading-progress';
import { Input } from '@/components/ui/input';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { toast } from 'sonner';
import { IdentitySelectorSearch } from './identity-selector-search';
import { useTranslation } from 'react-i18next';
import { VList } from 'virtua';
import { isNullOrWhiteSpace } from '@/extensions';
import { SecretInput } from '@/components/secret-input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import IdentityAvatar from '@/components/identity-avatar';

// The component props you want to require
export interface UpsertIdentityModalProps {
  className?: string;
  showDialog: boolean;
  onClose: () => void;
  onSave: () => void;
  identity?: IdentityItem;
}

// Create a function to get the form schema with translations
export const createFormSchema = (t: (key: string) => string) =>
  z
    .object({
      name: z.string().optional(),
      displayName: z.string().nonempty(t('Display name is required')),
      type: z.enum(['user', 'group', 'application']),
      role: z.enum(['user', 'administrator']),
      assigned: z.array(z.string()).optional(),
      accessMode: z.enum(['readOnly', 'collaborate', 'fullControl']).default('readOnly').optional()
    })
    .superRefine((data, ctx) => {
      if ((!data.name || isNullOrWhiteSpace(data.name)) && data.type !== 'application') {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: t('Name is required'),
          path: ['name']
        });
      }
    });

// The model (M) in M-V-VM (logic, behavior side effects)
export function useUpsertIdentityModalModel({ identity, onClose, onSave }: UpsertIdentityModalProps) {
  const { t } = useTranslation();
  const { data, error, isFetching, reset, fetchAsync } = useIdentity('');
  const {
    isFetching: isUpsertFetching,
    error: upsertError,
    postAsync: upsertFetchAsync,
    data: upsertData
  } = useUpsertIdentity();
  const editing = !!identity;

  const formSchema = createFormSchema(t);
  // `values` makes useGet.data the form's single source of truth on edit, avoiding
  // a useEffect that races fetchAsync. keepDirtyValues preserves in-progress user
  // edits if the GET resolves after they start typing.
  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      name: '',
      displayName: '',
      type: 'user',
      role: 'user',
      accessMode: 'readOnly',
      assigned: []
    },
    values: data,
    resetOptions: { keepDirtyValues: true }
  });

  const type = form.watch('type');

  useEffect(() => {
    if (identity) {
      fetchAsync('', identity.name);
    }
  }, [identity]);

  useEffect(() => {
    if (type && !editing) {
      form.setValue('assigned', []);
    }
  }, [type]);

  useEffect(() => {
    if (type === 'application' && (!data || !data.name)) {
      form.setValue('name', '');
    }
  }, [type, data?.name]);

  function onSubmit(values: z.infer<typeof formSchema>) {
    return new Promise<void>((resolve, reject) => {
      const savePromise = async () => {
        let error = '';
        try {
          const response = await upsertFetchAsync(values);
          error = response.error ?? '';
          if (!error) {
            if (!response.data?.clientSecret) {
              onSave();
            }
            form.reset();
            reset();
          } else {
            throw error;
          }
        } catch (error) {
          throw error;
        }
      };
      toast.promise(savePromise, {
        loading: t('Updating...'),
        success: () => {
          resolve();
          return t('Identity updated successfully');
        },
        error: () => {
          reject();
          return t('Error updating identity');
        }
      });
    });
  }

  const onCloseUpsertIdentity = () => {
    onSave();
  };

  const onAddAssigned = (identityName: string[]) => {
    // New array reference + shouldDirty: needed so form.watch re-renders and
    // keepDirtyValues protects the edit against any future data resync.
    const current = form.getValues('assigned') ?? [];
    const next = [...current, ...identityName.filter((name) => !current.includes(name))];
    form.setValue('assigned', next, { shouldDirty: true });
  };

  const onRemoveAssigned = (identityName: string) => {
    const current = form.getValues('assigned') ?? [];
    const next = current.filter((item) => item !== identityName);
    form.setValue('assigned', next, { shouldDirty: true });
  };

  function onCancel() {
    onClose();
    form.reset();
    reset();
  }

  return {
    editing,
    error,
    form,
    onSubmit,
    isFetching,
    onCancel,
    onRemoveAssigned,
    onAddAssigned,
    isUpsertFetching,
    upsertError,
    upsertData,
    onCloseUpsertIdentity
  };
}

// The pure view (V) in M-V-VM (no logic, no side effects)
export default function UpsertIdentityModalView({
  className,
  showDialog,
  onCancel,
  form,
  editing,
  error,
  onSubmit,
  isFetching,
  onRemoveAssigned,
  onAddAssigned,
  isUpsertFetching,
  upsertError,
  upsertData,
  onCloseUpsertIdentity
}: UpsertIdentityModalProps & {
  showDialog: boolean;
  onCancel: () => void;
  form: UseFormReturn<any>;
  editing: boolean;
  error?: string;
  onSubmit: (values: any) => void;
  isFetching: boolean;
  onRemoveAssigned: (identityName: string) => void;
  onAddAssigned: (identityName: string[]) => void;
  isUpsertFetching: boolean;
  upsertError?: string;
  upsertData?: IdentityWithAssignedItem;
  onCloseUpsertIdentity: () => void;
}) {
  const { t } = useTranslation();
  const type = form.watch('type');
  const assigned = form.watch('assigned');
  const formDisabled = isFetching || isUpsertFetching;
  const [identitySearch, setIdentitySearch] = useState('');
  const identitesToList = identitySearch ? assigned?.filter((item: string) => item.includes(identitySearch)) : assigned;
  return (
    <div className={className}>
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)}>
          <AlertDialog open={showDialog}>
            {upsertData && upsertData.clientSecret && (
              <AlertDialogContent className="p-4 sm:max-w-screen lg:max-w-6/10">
                <AlertDialogHeader>
                  <AlertDialogTitle>{'Client secret created'}</AlertDialogTitle>
                </AlertDialogHeader>
                <div className="flex flex-col gap-3 p-4 bg-dialog-content-glass">
                  <span>
                    {t(
                      "A secret has been created associated with the new service. Please keep it safe, as once you close this dialog, you won't be able to see it again."
                    )}
                  </span>
                  <FormLabel>{t('Client Id')}</FormLabel>
                  <Input value={upsertData.name} readOnly />
                  <FormLabel>{t('Secret')}</FormLabel>
                  <SecretInput value={upsertData.clientSecret} />
                </div>
                <AlertDialogFooter>
                  <AlertDialogAction onClick={onCloseUpsertIdentity}>{t('Close')}</AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            )}
            {(!upsertData || !upsertData.clientSecret) && (
              <AlertDialogContent className="p-4 lg:max-w-[900px]">
                <AlertDialogHeader>
                  <AlertDialogTitle>{editing ? t('Edit identity') : t('New identity')}</AlertDialogTitle>
                  <AlertDialogDescription>
                    {editing ? t('Update identity information') : t('Provide the new identity information')}
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <>
                  {(isFetching || isUpsertFetching) && <LoadingProgress />}
                  {(error || upsertError) && (
                    <Alert variant="destructive" className="p-2 flex">
                      <AlertCircle className="size-4" />
                      <AlertTitle>{t('Something went wrong. Try again...')}</AlertTitle>
                      <AlertDescription>{error || upsertError}</AlertDescription>
                    </Alert>
                  )}
                  <div className="flex flex-col gap-8 p-4 bg-dialog-content-glass">
                    <div className="flex flex-col lg:flex-row align-top justify-start gap-4">
                      <div className="flex-1">
                        <FormField
                          control={form.control}
                          name="name"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>{t('Name')}</FormLabel>
                              <FormControl>
                                <Input
                                  autoFocus
                                  placeholder={t('Identity name')}
                                  {...field}
                                  disabled={editing || formDisabled || type === 'application'}
                                />
                              </FormControl>
                              <FormDescription>
                                {t(
                                  'Enter a unique name for the identity. This will be used to identify the identity in the system.'
                                )}
                              </FormDescription>
                              <FormMessage />
                            </FormItem>
                          )}
                        />
                      </div>
                      <div className="flex-1">
                        <FormField
                          control={form.control}
                          name="displayName"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>{t('Display name')}</FormLabel>
                              <FormControl>
                                <Input placeholder={t('Your full name')} {...field} disabled={formDisabled} />
                              </FormControl>
                              <FormDescription>
                                {t('Enter your full name. This helps others recognize you easily.')}
                              </FormDescription>
                              <FormMessage />
                            </FormItem>
                          )}
                        />
                      </div>
                    </div>

                    <div className="flex flex-col lg:flex-row align-top justify-start gap-4">
                      <div className="flex-1">
                        <FormField
                          control={form.control}
                          name="type"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>{t('Type')}</FormLabel>
                              <FormControl>
                                <ToggleGroup
                                  type="single"
                                  variant="outline"
                                  className="w-full my-2"
                                  value={field.value}
                                  onValueChange={field.onChange}
                                  disabled={editing || formDisabled}>
                                  <ToggleGroupItem value="user" aria-label={t('Toggle User')}>
                                    <span className="flex items-center pl-2 pr-2">
                                      <User className="mr-2" /> {t('User')}
                                    </span>
                                  </ToggleGroupItem>
                                  <ToggleGroupItem value="group" aria-label={t('Toggle Group')}>
                                    <span className="flex items-center pl-2 pr-2">
                                      <Users className="mr-2" /> {t('Group')}
                                    </span>
                                  </ToggleGroupItem>
                                  <ToggleGroupItem value="application" aria-label={t('Toggle Application')}>
                                    <span className="flex items-center pl-2 pr-2">
                                      <AppWindowMac className="mr-2" /> {t('Application')}
                                    </span>
                                  </ToggleGroupItem>
                                </ToggleGroup>
                              </FormControl>
                              <FormDescription>
                                {t('Select the identity type. Group if it can contain other identities.')}
                              </FormDescription>
                              <FormMessage />
                            </FormItem>
                          )}
                        />
                      </div>
                      <div className="flex-1">
                        <FormField
                          control={form.control}
                          name="role"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>{t('Role')}</FormLabel>
                              <FormControl>
                                <ToggleGroup
                                  type="single"
                                  variant="outline"
                                  className="w-full my-2"
                                  value={field.value}
                                  onValueChange={field.onChange}
                                  disabled={formDisabled}>
                                  <ToggleGroupItem value="user" aria-label={t('Toggle User')}>
                                    <span className="flex items-center pl-2 pr-2">
                                      <Contact className="mr-2" /> {t('User')}
                                    </span>
                                  </ToggleGroupItem>
                                  <ToggleGroupItem value="administrator" aria-label={t('Toggle Administrator')}>
                                    <span className="flex items-center pl-2 pr-2">
                                      <ShieldUser className="mr-2" /> {t('Administrator')}
                                    </span>
                                  </ToggleGroupItem>
                                </ToggleGroup>
                              </FormControl>
                              <FormDescription>
                                {t(
                                  'Select the role for this identity. The user has limited permissions, while the administrator has full access to the system.'
                                )}
                              </FormDescription>
                              <FormMessage />
                            </FormItem>
                          )}
                        />
                      </div>
                    </div>

                    {type === 'application' && (
                      <FormField
                        control={form.control}
                        name="accessMode"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>{t('Access mode')}</FormLabel>
                            <Select onValueChange={field.onChange} defaultValue={field.value}>
                              <FormControl className="flex flex-1 min-w-[200px] max-w">
                                <SelectTrigger>
                                  <SelectValue />
                                </SelectTrigger>
                              </FormControl>
                              <SelectContent>
                                <SelectItem value="readOnly">{t('Read only')}</SelectItem>
                                <SelectItem value="collaborate">{t('Collaborate')}</SelectItem>
                                <SelectItem value="fullControl">{t('Full control')}</SelectItem>
                              </SelectContent>
                            </Select>
                            <FormDescription>
                              {t('Select how your application will access this platform API services.')}
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    )}

                    <FormItem>
                      <FormLabel>
                        {type === 'group' ? t('Group members') : t('Member of')} ({assigned?.length})
                      </FormLabel>
                      {(assigned?.length ?? 0) > 0 && (
                        <Input
                          placeholder={t('Search for identities')}
                          value={identitySearch}
                          onChange={(e) => setIdentitySearch(e.target.value)}
                        />
                      )}

                      {(assigned?.length ?? 0) > 0 && (
                        <div className="flex flex-col gap-2">
                          <VList style={{ height: 200 }}>
                            {identitesToList?.map((identityName: string) => (
                              <div
                                key={identityName}
                                className="flex justify-between items-center gap-2 p-2 hover:bg-accent rounded-md w-full text-left">
                                <div className="flex items-center gap-2 text-accent-foreground text-sm">
                                  <IdentityAvatar name={identityName} type={'user' as IdentityType} size={32} />
                                  <div className="grid flex-1 text-left text-sm leading-tight">
                                    <span className="truncate font-semibold">{identityName}</span>
                                  </div>
                                  <Button
                                    variant="ghost"
                                    className="size-4 cursor-pointer"
                                    onClick={() => onRemoveAssigned(identityName)}>
                                    <X />
                                  </Button>
                                </div>
                              </div>
                            ))}
                          </VList>
                        </div>
                      )}

                      {(assigned?.length ?? 0) === 0 && (
                        <div className="flex items-center gap-2 ml-1">
                          <span className="text-muted-foreground text-sm">{t('No identities assigned')}</span>
                        </div>
                      )}

                      {!formDisabled && (
                        <IdentitySelectorSearch
                          onSelect={(value) => onAddAssigned([value.name])}
                          filterType={type === 'group' ? ['user', 'application'] : ['group']}
                        />
                      )}
                    </FormItem>
                  </div>
                </>
                <AlertDialogFooter>
                  <AlertDialogCancel onClick={onCancel}>{t('Cancel')}</AlertDialogCancel>
                  <AlertDialogAction disabled={formDisabled} onClick={form.handleSubmit(onSubmit)}>
                    {t('Save')}
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            )}
          </AlertDialog>
        </form>
      </Form>
    </div>
  );
}

// The ViewModel (VM) or final component in M-V-VM (composition of M and V)
export function UpsertIdentityModalContent(props: UpsertIdentityModalProps) {
  const model = useUpsertIdentityModalModel(props);

  return <UpsertIdentityModalView {...props} {...model} />;
}

export function UpsertIdentityModal(props: UpsertIdentityModalProps) {
  return <UpsertIdentityModalContent {...props} />;
}
