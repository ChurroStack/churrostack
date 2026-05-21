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
import { useCreateLlm } from '@/hooks/data/llms';
import { useNavigate } from 'react-router';

// The component props you want to require
export interface NewLlmDialogProps {
  showDialog: boolean;
  reload?: () => void;
  onClose: () => void;
}
const formSchema = z.object({
  name: z.string().max(254, 'Llm name cannot have more than 254 characters').min(1, 'Llm name is required')
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
export function useNewLlmDialogModel({ reload, onClose }: NewLlmDialogProps) {
  //const { reload: reloadLlms } = useLlmService();
  const { isFetching, isSuccess, isError, error, postAsync, reset } = useCreateLlm();
  const navigate = useNavigate();

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      name: ''
    },
    mode: 'onChange'
  });

  const clear = () => {
    form.reset();
    reset();
  };

  useEffect(() => {
    if (isSuccess && !isError) {
      onClose();
      clear();
      if (reload) {
        reload();
      }
    }
  }, [isSuccess, isError]);

  async function onSubmit(values: z.infer<typeof formSchema>) {
    const result = await postAsync({
      names: [values.name]
    });
    if (result.data && result.data.id && !result.error) {
      navigate(`/llms/${result.data.id}`); // Navigate to the newly created llm details page
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
export function NewLlmDialogView({
  showDialog,
  isError,
  isFetching,
  error,
  onOpenChange,
  form,
  onSubmit
}: NewLlmDialogProps & {
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
              <AlertDialogTitle>{t('New llm')}</AlertDialogTitle>
              <AlertDialogDescription>{t('Creates a new llm')}</AlertDialogDescription>
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
                    <FormLabel>{t('Llm name')}</FormLabel>
                    <FormControl>
                      <div className="flex flex-row items-center">
                        <Input
                          id="name"
                          placeholder={t('Enter the llm name...')}
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
export function NewLlmDialogContent(props: NewLlmDialogProps) {
  const model = useNewLlmDialogModel(props);
  return <NewLlmDialogView {...props} {...model} />;
}

export function NewLlmDialog(props: NewLlmDialogProps) {
  return <NewLlmDialogContent {...props} />;
}
