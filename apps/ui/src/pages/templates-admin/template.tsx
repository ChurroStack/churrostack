import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Separator } from '@/components/ui/separator';
import { formatDateTime } from '@/extensions';
import { useGetTemplate, useUpdateTemplate } from '@/hooks/data/templates';
import { AlertCircle, Save, FileBraces, CheckCircle2Icon } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { Spinner } from '@/components/ui/spinner';
import { z } from 'zod';
import { Form, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { useForm } from 'react-hook-form';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import Editor from '@monaco-editor/react';
import { useTheme } from 'next-themes';

const formSchema = z.object({
  content: z.string()
});

const Template = () => {
  const { t } = useTranslation();
  const { id } = useParams();
  const { fetchAsync, data, isFetching, error } = useGetTemplate(id);
  const { putAsync: updateAsync, isFetching: isUpdating, error: updateError, isSuccess } = useUpdateTemplate(id);
  const [template, setTemplate] = useState(data);
  const { resolvedTheme } = useTheme();

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      content: template?.content || ''
    },
    mode: 'onChange'
  });

  const content = form.watch('content');

  useEffect(() => {
    fetchAsync('').then((response) => {
      form.setValue('content', response?.data?.content || '');
      setTemplate(response?.data);
    });
  }, [id]);

  const updateTemplate = async () => {
    const isValid = await form.trigger();
    if (!isValid) {
      return;
    }
    const result = await updateAsync(content || '', '');
    if (!result.error) {
      setTemplate(result.data);
    }
  };

  if (isFetching || !template) {
    return <LoadingSkeleton maxCards={9} />;
  }

  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading templates')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between px-4 py-2 pb-0 max-w-full items-center">
        <h1 className="text-2xl font-bold flex flex-row items-center gap-2">
          <FileBraces /> {template?.name}
        </h1>
        <div className="flex flex-row items-center gap-2"></div>
      </div>
      <Separator className="my-2" />
      {updateError && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error updating template')}</AlertTitle>
          <AlertDescription>{updateError}</AlertDescription>
        </Alert>
      )}
      {isSuccess && (
        <div className="flex flex-col mb-4 px-4">
          <Alert className="text-green-800 bg-green-50">
            <CheckCircle2Icon className="size-4" />
            <AlertTitle>{t('Template updated successfully')}</AlertTitle>
            <AlertDescription>{t('The template has been updated successfully.')}</AlertDescription>
          </Alert>
        </div>
      )}
      <Form {...form}>
        <form className="flex flex-col min-h-0 flex-1">
          <div className="flex flex-col min-h-0 flex-1 p-4 pb-0 gap-2">
            <FormField
              control={form.control}
              name="content"
              render={({ field }) => (
                <FormItem className="flex flex-col min-h-0 flex-1">
                  <FormLabel>{t('Content')}</FormLabel>
                  <div className="flex-1 min-h-0 border rounded-md overflow-hidden">
                    <Editor
                      language="yaml"
                      theme={resolvedTheme === 'dark' ? 'vs-dark' : 'vs'}
                      value={field.value}
                      onChange={(value) => field.onChange(value ?? '')}
                      loading={<Spinner />}
                      options={{
                        minimap: { enabled: false },
                        fontSize: 14,
                        lineNumbers: 'on',
                        scrollBeyondLastLine: false,
                        wordWrap: 'on',
                        automaticLayout: true,
                        tabSize: 2,
                        fontFamily: "'JetBrains Mono', 'Fira Code', monospace"
                      }}
                    />
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
          <div className="flex flex-col gap-4 p-4 pt-2">
            <FieldGroup className="flex flex-row gap-2">
              <Field>
                <FieldLabel>{t('Created at')}</FieldLabel>
                <Input readOnly value={formatDateTime(template?.createdAt ?? '')} />
              </Field>
              <Field>
                <FieldLabel>{t('Created by')}</FieldLabel>
                <Input readOnly value={template?.createdBy?.displayName ?? template?.createdBy?.name} />
              </Field>
              <Field>
                <FieldLabel>{t('Modified at')}</FieldLabel>
                <Input readOnly value={formatDateTime(template?.modifiedAt ?? '')} />
              </Field>
              <Field>
                <FieldLabel>{t('Modified by')}</FieldLabel>
                <Input readOnly value={template?.modifiedBy?.displayName ?? template?.modifiedBy?.name} />
              </Field>
            </FieldGroup>
            <div className="flex flex-row justify-end">
              <Button
                variant="default"
                onClick={(e) => {
                  e.preventDefault();
                  updateTemplate();
                }}
                disabled={isUpdating}>
                {isUpdating ? <Spinner /> : <Save />} {t('Update template')}
              </Button>
            </div>
          </div>
        </form>
      </Form>
    </div>
  );
};

export default Template;
