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
import { useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { useForm } from 'react-hook-form';
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Spinner } from '@/components/ui/spinner';
import { useCreateTemplate } from '@/hooks/data/templates';
import { useNavigate } from 'react-router';
import { Textarea } from '@/components/ui/textarea';

// The component props you want to require
export interface NewTemplateDialogProps {
  showDialog: boolean;
  reload?: () => void;
  onClose: () => void;
}
const formSchema = z.object({
  content: z.string().min(1, 'Template content is required')
});
// The model (M) in M-V-VM (logic, behavior side effects)
export function useNewTemplateDialogModel({ reload, onClose }: NewTemplateDialogProps) {
  //const { reload: reloadTemplates } = useTemplateService();
  const { isFetching, isSuccess, isError, error, postAsync, reset } = useCreateTemplate();
  const navigate = useNavigate();

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      content: ''
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
    const result = await postAsync(values.content);
    if (!result.error) {
      navigate(`/templates/${result.data?.name}:${result.data?.target}`);
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
export function NewTemplateDialogView({
  showDialog,
  isError,
  isFetching,
  error,
  onOpenChange,
  form,
  onSubmit
}: NewTemplateDialogProps & {
  onOpenChange: (open: boolean) => void;
  isError: boolean;
  isFetching: boolean;
  error?: string;
  onSubmit: (values: z.infer<typeof formSchema>) => Promise<void>;
  form: ReturnType<typeof useForm<z.infer<typeof formSchema>>>;
}) {
  const { t } = useTranslation();

  return (
    <AlertDialog open={showDialog} onOpenChange={onOpenChange}>
      <AlertDialogContent className="sm:max-w-125">
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)}>
            <AlertDialogHeader>
              <AlertDialogTitle>{t('New template')}</AlertDialogTitle>
              <AlertDialogDescription>{t('Creates a new template')}</AlertDialogDescription>
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
                name="content"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('Template content')}</FormLabel>
                    <FormControl>
                      <div className="flex flex-row items-center">
                        <Textarea
                          id="content"
                          placeholder={t('Enter the template content...')}
                          className="bg-button-glass max-h-50"
                          {...field}
                          ref={(el) => {
                            field.ref(el);
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
export function NewTemplateDialogContent(props: NewTemplateDialogProps) {
  const model = useNewTemplateDialogModel(props);
  return <NewTemplateDialogView {...props} {...model} />;
}

export function NewTemplateDialog(props: NewTemplateDialogProps) {
  return <NewTemplateDialogContent {...props} />;
}
