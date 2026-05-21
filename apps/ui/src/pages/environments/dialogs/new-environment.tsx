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
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { useForm } from 'react-hook-form';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Spinner } from '@/components/ui/spinner';
import { useCreateEnvironment } from '@/hooks/data/environments';
import { useNavigate } from 'react-router';

// The component props you want to require
export interface NewEnvironmentDialogProps {
  showDialog: boolean;
  reload?: () => void;
  onClose: () => void;
}
const formSchema = z.object({
  name: z
    .string()
    .max(254, 'Environment name cannot have more than 254 characters')
    .min(1, 'Environment name is required')
  // private: z.boolean().default(false),
  // members: z
  //   .array(
  //     z.object({
  //       name: z.string(),
  //       displayName: z.string(),
  //       type: z.string(),
  //       permission: z.number()
  //     })
  //   )
  //   .optional()
});
// The model (M) in M-V-VM (logic, behavior side effects)
export function useNewEnvironmentDialogModel({ reload, onClose }: NewEnvironmentDialogProps) {
  //const { reload: reloadEnvironments } = useEnvironmentService();
  const { isFetching, isSuccess, isError, error, postAsync, reset } = useCreateEnvironment();
  const navigate = useNavigate();

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      name: ''
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
      name: values.name
    });
    if (!result.error) {
      navigate(`/environments/${values.name}`);
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
export function NewEnvironmentDialogView({
  showDialog,
  isError,
  isFetching,
  error,
  onOpenChange,
  form,
  onSubmit
}: NewEnvironmentDialogProps & {
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
              <AlertDialogTitle>{t('New environment')}</AlertDialogTitle>
              <AlertDialogDescription>{t('Creates a new environment')}</AlertDialogDescription>
            </AlertDialogHeader>
            {isError && (
              <Alert className="mb-4" variant="destructive">
                <AlertCircle className="size-4" />
                <AlertTitle>{t('Error')}</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <div className="flex flex-col gap-4 p-4 my-4 bg-dialog-content-glass">
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('Environment name')}</FormLabel>
                    <FormControl>
                      <div className="flex flex-row items-center">
                        <Input
                          id="name"
                          placeholder={t('Enter the environment name...')}
                          {...field}
                          ref={(el) => {
                            field.ref(el);
                            inputRef.current = el;
                          }}
                        />
                      </div>
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
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
export function NewEnvironmentDialogContent(props: NewEnvironmentDialogProps) {
  const model = useNewEnvironmentDialogModel(props);
  return <NewEnvironmentDialogView {...props} {...model} />;
}

export function NewEnvironmentDialog(props: NewEnvironmentDialogProps) {
  return <NewEnvironmentDialogContent {...props} />;
}
