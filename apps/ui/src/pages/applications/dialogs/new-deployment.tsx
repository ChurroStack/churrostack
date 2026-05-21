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
import { AlertCircle } from 'lucide-react';
import { useEffect, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { Controller, useForm } from 'react-hook-form';
import { Form, FormControl, FormDescription, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Spinner } from '@/components/ui/spinner';
import { useCreateApplicationDeployment } from '@/hooks/data/applications';
import IdentityPicker from '@/pickers/identity-picker';

// The component props you want to require
export interface NewDeploymentDialogProps {
  appName: string;
  showDialog: boolean;
  reload?: () => void;
  onClose: () => void;
}
const formSchema = z.object({
  identityName: z.string().max(254, 'Identity cannot have more than 254 characters').min(1, 'Identity name is required')
});
// The model (M) in M-V-VM (logic, behavior side effects)
export function useNewDeploymentDialogModel({ appName, reload, onClose }: NewDeploymentDialogProps) {
  //const { reload: reloadDeployments } = useDeploymentService();
  const { isFetching, isSuccess, isError, error, postAsync, reset } = useCreateApplicationDeployment(appName);

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      identityName: undefined
    },
    mode: 'onChange'
  });

  useEffect(() => {
    if (isSuccess && !isError) {
      onClose();
      if (reload) {
        reload();
      }
    }
  }, [isSuccess, isError]);

  const clear = () => {
    form.reset();
    reset();
  };

  async function onSubmit(values: z.infer<typeof formSchema>) {
    const result = await postAsync({
      identityName: values.identityName
    });
    if (!result.error) {
      reload?.();
      clear();
      onClose();
    }
  }

  const onOpenChange = (open: boolean) => {
    clear();
    if (open === false && onClose) {
      onClose();
    }
  };

  return {
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
export function NewDeploymentDialogView({
  showDialog,
  isError,
  isFetching,
  error,
  onOpenChange,
  form,
  onSubmit
}: NewDeploymentDialogProps & {
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
          <form onSubmit={form.handleSubmit(onSubmit)}>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('New deployment')}</AlertDialogTitle>
              <AlertDialogDescription>{t('Creates a new deployment')}</AlertDialogDescription>
            </AlertDialogHeader>
            {isError && (
              <Alert className="mb-4" variant="destructive">
                <AlertCircle className="size-4" />
                <AlertTitle>{t('Error')}</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <div className="flex flex-col gap-4 p-4 my-4 bg-dialog-content-glass">
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
            </div>

            <AlertDialogFooter>
              <AlertDialogCancel>{t('Cancel')}</AlertDialogCancel>
              <Button type="submit" disabled={isFetching}>
                {isFetching && <Spinner className="text-white-600 size-5" />} {t('Create')}
              </Button>
            </AlertDialogFooter>
          </form>
        </Form>
      </AlertDialogContent>
    </AlertDialog>
  );
}

// The ViewModel (VM) or final component in M-V-VM (composition of M and V)
export function NewDeploymentDialogContent(props: NewDeploymentDialogProps) {
  const model = useNewDeploymentDialogModel(props);
  return <NewDeploymentDialogView {...props} {...model} />;
}

export function NewDeploymentDialog(props: NewDeploymentDialogProps) {
  return <NewDeploymentDialogContent {...props} />;
}
