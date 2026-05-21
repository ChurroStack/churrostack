import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle, ChevronDownIcon } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { Controller, useForm } from 'react-hook-form';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Spinner } from '@/components/ui/spinner';
import { useCreateApiKey } from '@/hooks/data/api-keys';
import { useNavigate } from 'react-router';
import InputWithCopy from '@/components/input-with-copy';
import { useProfile } from '@/hooks/data/profile';
import IdentityPicker from '@/pickers/identity-picker';
import { Textarea } from '@/components/ui/textarea';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import { formatDateTime } from '@/extensions';

// The component props you want to require
export interface NewApiKeyDialogProps {
  showDialog: boolean;
  reload?: () => void;
  onClose: () => void;
}
const formSchema = z.object({
  identityName: z.string().max(254, 'ApiKey name cannot have more than 254 characters').optional(),
  description: z.string().optional(),
  expiresAt: z.string().optional()
});

const getDefaultExpirationDate = () => {
  const date = new Date();
  date.setDate(date.getDate() + 90);
  return date.toISOString();
};

// The model (M) in M-V-VM (logic, behavior side effects)
export function useNewApiKeyDialogModel({ reload, onClose }: NewApiKeyDialogProps) {
  //const { reload: reloadApiKeys } = useApiKeyService();
  const { isFetching, isError, error, postAsync, reset } = useCreateApiKey();
  const [apiKey, setApiKey] = useState<string | null>(null);
  const [apiKeyId, setApiKeyId] = useState<string | null>(null);
  const { profile } = useProfile();

  const navigate = useNavigate();

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      identityName: profile?.name,
      expiresAt: getDefaultExpirationDate()
    },
    mode: 'onChange'
  });

  const clear = () => {
    setApiKey(null);
    setApiKeyId(null);
    form.reset();
    reset();
  };

  async function onSubmit(values: z.infer<typeof formSchema>) {
    if (apiKey) {
      navigate(`/keys/${apiKeyId}`);
      onClose?.();
      clear();
      reload?.();
    } else {
      const result = await postAsync({
        identityName: !values.identityName || values.identityName === '' ? profile?.name : values.identityName,
        expiresAt: !values.expiresAt || values.expiresAt === '' ? undefined : values.expiresAt,
        description: values.description
      });
      if (result?.data?.apiKey) {
        setApiKey(result.data.apiKey);
        setApiKeyId(result.data.id);
      }
    }
  }

  const onOpenChange = (open: boolean) => {
    clear();
    if (open === false && onClose) {
      onClose();
    }
  };

  return {
    apiKey,
    apiKeyId,
    clear,
    onOpenChange,
    isFetching,
    isError,
    error,
    onSubmit,
    form
  };
}

// The pure view (V) in M-V-VM (no logic, no side effects)
export function NewApiKeyDialogView({
  apiKey,
  clear,
  showDialog,
  isError,
  isFetching,
  error,
  onOpenChange,
  form,
  onSubmit
}: NewApiKeyDialogProps & {
  apiKey: string | null;
  apiKeyId: string | null;
  clear: () => void;
  onOpenChange: (open: boolean) => void;
  isError: boolean;
  isFetching: boolean;
  error?: string;
  onSubmit: (values: z.infer<typeof formSchema>) => Promise<void>;
  form: ReturnType<typeof useForm<z.infer<typeof formSchema>>>;
}) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (showDialog) {
      setTimeout(() => {
        inputRef.current?.focus();
      }, 100);
    }
  }, [showDialog]);

  return (
    <AlertDialog open={showDialog} onOpenChange={onOpenChange}>
      <AlertDialogContent className="sm:max-w-125">
        <Form {...form}>
          <form
            onSubmit={(e) => {
              e.stopPropagation();
              e.preventDefault();
            }}>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('New key')}</AlertDialogTitle>
              <AlertDialogDescription>{t('Creates a new key')}</AlertDialogDescription>
            </AlertDialogHeader>
            {isError && (
              <Alert className="mb-4" variant="destructive">
                <AlertCircle className="size-4" />
                <AlertTitle>{t('Error')}</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <div className="flex flex-col gap-4 py-4">
              {apiKey && (
                <div className="flex flex-col gap-2">
                  <Alert>
                    <AlertCircle className="size-4" />
                    <AlertTitle>{t('Your API key was generated successfully')}</AlertTitle>
                    <AlertDescription>
                      <span className="text-sm flex flex-row mt-2 w-full">
                        {t("Please copy and save this key securely. You won't be able to see it again.")}
                      </span>
                    </AlertDescription>
                  </Alert>
                  <InputWithCopy value={apiKey} />
                </div>
              )}
              {!apiKey && (
                <FormItem>
                  <FormLabel>{t('Target identity')}</FormLabel>
                  <FormControl>
                    <Controller
                      name={`identityName`}
                      control={form.control}
                      render={({ field: identityNameField }) => (
                        <IdentityPicker className="flex h-9 flex-1 w-full" {...identityNameField} />
                      )}
                    />
                  </FormControl>
                  <FormDescription>{t('The identity this key will be associated with.')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}

              {!apiKey && (
                <FormField
                  control={form.control}
                  name="expiresAt"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('Key expiration')}</FormLabel>
                      <FormDescription>{t('Set an expiration date for this key (default 90 days).')}</FormDescription>
                      <FormControl>
                        <Popover>
                          <PopoverTrigger asChild>
                            <Button
                              variant="outline"
                              data-empty={!field.value}
                              className="data-[empty=true]:text-muted-foreground justify-between text-left font-normal">
                              {field.value ? formatDateTime(field.value) : <span>{t('Pick a date')}</span>}
                              <ChevronDownIcon />
                            </Button>
                          </PopoverTrigger>
                          <PopoverContent className="w-auto p-0" align="start">
                            <Calendar
                              mode="single"
                              selected={field.value ? new Date(field.value) : undefined}
                              onSelect={(value) => field.onChange(value?.toISOString())}
                            />
                          </PopoverContent>
                        </Popover>
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              {!apiKey && (
                <FormField
                  control={form.control}
                  name="description"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('Key description')}</FormLabel>
                      <FormDescription>
                        {t('Describe the purpose of this key so you can identify it later.')}
                      </FormDescription>
                      <FormControl>
                        <Textarea placeholder={t('Describe the purpose of this key')} {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
            </div>

            <AlertDialogFooter>
              <AlertDialogCancel onClick={() => clear()}>{t('Cancel')}</AlertDialogCancel>
              <Button
                disabled={isFetching}
                onClick={(e) => {
                  e.stopPropagation();
                  e.preventDefault();
                  onSubmit(form.getValues());
                }}>
                {isFetching && <Spinner className="text-white-600 size-5" />} {apiKey ? t('Close') : t('Create')}
              </Button>
            </AlertDialogFooter>
          </form>
        </Form>
      </AlertDialogContent>
    </AlertDialog>
  );
}

// The ViewModel (VM) or final component in M-V-VM (composition of M and V)
export function NewApiKeyDialogContent(props: NewApiKeyDialogProps) {
  const model = useNewApiKeyDialogModel(props);
  return <NewApiKeyDialogView {...props} {...model} />;
}

export function NewApiKeyDialog(props: NewApiKeyDialogProps) {
  return <NewApiKeyDialogContent {...props} />;
}
