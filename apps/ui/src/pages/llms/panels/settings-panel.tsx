import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useGetLlmDestinationModels, useTestLlmDestination, useUpdateLlm, type LlmItem } from '@/hooks/data/llms';
import { AlertCircle, Plus, Save, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { useFieldArray, useForm, type UseFormReturn } from 'react-hook-form';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { Field, FieldDescription, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { formatDateTime } from '@/extensions';
import { Form, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { toast } from 'sonner';
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useEffect, useState } from 'react';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue
} from '@/components/ui/select';
import ApplicationPicker from '@/pickers/application-picker';
import { Textarea } from '@/components/ui/textarea';

const priceField = z.preprocess(
  (v) => (v === '' || v === null || v === undefined ? undefined : v),
  z.coerce.number().nonnegative().optional()
);

const llmDestination = z.object({
  uri: z.string().min(1, 'Enter the URL endpoint').url('Must be a valid URL'),
  model: z.string().min(1, 'Model name is required'),
  apiKey: z.string().optional(),
  patch: z.string().optional(),
  inputTokenPricePer1M: priceField,
  outputTokenPricePer1M: priceField
});

const formSchema = z.object({
  names: z
    .array(z.string().min(1, 'Cannot be empty'))
    .min(1, 'At least one name is required')
    .max(5, 'Maximum 5 names allowed'),
  destination: z.array(llmDestination).min(1, 'At least one destination is required')
});

const DestinationForm = ({
  id,
  form,
  index,
  remove
}: {
  id: string;
  form: UseFormReturn<z.input<typeof formSchema>, unknown, z.output<typeof formSchema>>;
  index: number;
  remove: (index: number) => void;
}) => {
  const { t } = useTranslation();
  const { isFetching, postAsync, error, isSuccess } = useTestLlmDestination(id);
  const {
    isFetching: isFetchingModels,
    postAsync: fetchModelsAsync,
    error: modelsError
  } = useGetLlmDestinationModels(id);
  const [endpointType, setEndpointType] = useState<'internal' | 'external'>(
    form.getValues(`destination.${index}.uri`)?.startsWith('http') ? 'external' : 'internal'
  );
  const [selectedApp, setSelectedApp] = useState<string | null>(null);

  useEffect(() => {
    setEndpointType(form.getValues(`destination.${index}.uri`)?.startsWith('http') ? 'external' : 'internal');
    setSelectedApp(form.getValues(`destination.${index}.uri`));
  }, [form, index]);

  return (
    <Card className="w-full shadow-none">
      <CardHeader>
        <CardTitle>{t('LLM endpoint')}</CardTitle>
        <CardDescription>
          {t('Configure the LLM endpoint (OpenAI, Azure, or self-hosted) that supports your LLM.')}
        </CardDescription>
        <CardAction>
          <ConfirmDialog
            title={t('Delete selected LLM endpoint')}
            description={t('Are you sure want to delete selected LLM endpoint?')}
            acceptText={t('Delete')}
            acceptVariant="destructive"
            onAccept={() => remove(index)}>
            <Button variant="ghost">
              <Trash2 />
            </Button>
          </ConfirmDialog>
        </CardAction>
      </CardHeader>
      <CardContent>
        {error && (
          <Alert variant="destructive" className="mb-4">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error testing endpoint')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {modelsError && (
          <Alert variant="destructive" className="mb-4">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error fetching models')}</AlertTitle>
            <AlertDescription>{modelsError}</AlertDescription>
          </Alert>
        )}
        {isSuccess && (
          <Alert className="mb-4">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('The endpoint is working as expected')}</AlertTitle>
          </Alert>
        )}
        <form>
          <div className="flex flex-col gap-6">
            <div className="grid gap-2">
              <Label htmlFor="url">{t('Endpoint type')}</Label>
              <Select
                value={endpointType}
                onValueChange={(value) => {
                  setEndpointType(value as 'internal' | 'external');
                }}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder={t('Select the endpoint type')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    <SelectLabel>{t('Endpoint Type')}</SelectLabel>
                    <SelectItem value={'internal'}>{t('Internal')}</SelectItem>
                    <SelectItem value={'external'}>{t('External')}</SelectItem>
                  </SelectGroup>
                </SelectContent>
              </Select>
            </div>
            {endpointType === 'external' && (
              <div className="grid gap-2">
                <Label htmlFor="url">{t('Url')}</Label>
                <Input
                  id="url"
                  type="url"
                  placeholder="https://api.openai.com/v1"
                  required
                  {...form.register(`destination.${index}.uri`)}
                />
                <FieldDescription>
                  {t(
                    'The base URL for the LLM endpoint. For Azure use this pattern: https://<endpoint>.openai.azure.com/openai/deployments/<deployment>?api-version=2024-06-01'
                  )}
                </FieldDescription>
              </div>
            )}
            {endpointType === 'internal' && (
              <div className="grid gap-2">
                <Label htmlFor="application">{t('Application')}</Label>
                <div className="flex flex-row gap-2 flex-1 w-full">
                  <ApplicationPicker
                    className="flex-1"
                    value={selectedApp ?? ''}
                    autoSelect
                    onChange={(value) => {
                      setSelectedApp(value);
                      form.setValue(`destination.${index}.uri`, `internal://${value}/v1`);
                    }}
                  />
                  <Button
                    variant="secondary"
                    onClick={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      fetchModelsAsync(
                        form.getValues(`destination.${index}.uri`),
                        form.getValues(`destination.${index}.apiKey`)
                      ).then((result) => {
                        if (result && result.data && result.data.data && result.data.data.length > 0) {
                          form.setValue(`destination.${index}.model`, result.data.data[0].id);
                        }
                      });
                    }}>
                    {isFetchingModels && <Spinner />} {t('Fetch models')}
                  </Button>
                </div>
              </div>
            )}
            <div className="grid gap-2">
              <Label htmlFor="apiKey">{t('Api Key')}</Label>
              <Input id="apiKey" type="password" placeholder="" {...form.register(`destination.${index}.apiKey`)} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="model">{t('Model')}</Label>
              <div className="flex flex-row gap-2">
                <Input
                  id="model"
                  type="text"
                  placeholder="gpt-4"
                  required
                  {...form.register(`destination.${index}.model`)}
                />
                {/* <Select
                  value={form.getValues(`destination.${index}.model`)}
                  onValueChange={(value) => {
                    if (value && value != '') form.setValue(`destination.${index}.model`, value);
                  }}>
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder={t('Select a branch')} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectGroup>
                      <SelectLabel>{t('Branches')}</SelectLabel>
                      {models.map((model) => (
                        <SelectItem key={model} value={model}>
                          {model}
                        </SelectItem>
                      ))}
                    </SelectGroup>
                  </SelectContent>
                </Select>
                <Button
                  variant="secondary"
                  onClick={() => {
                    postAsync(
                      form.getValues(`destination.${index}.uri`),
                      form.getValues(`destination.${index}.apiKey`)
                    );
                  }}>
                  {isFetching && <Spinner />} {t('Fetch models')}
                </Button> */}
                <Button
                  variant="secondary"
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    postAsync(
                      form.getValues(`destination.${index}.uri`),
                      form.getValues(`destination.${index}.model`),
                      form.getValues(`destination.${index}.apiKey`)
                    );
                  }}>
                  {isFetching && <Spinner />} {t('Test endpoint')}
                </Button>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor={`inputPrice-${index}`}>{t('Input price (USD / 1M tokens)')}</Label>
                <Input
                  id={`inputPrice-${index}`}
                  type="number"
                  step="0.0001"
                  min="0"
                  placeholder="0.00"
                  {...form.register(`destination.${index}.inputTokenPricePer1M`)}
                />
                <FieldDescription>
                  {t('Cost per 1,000,000 input (prompt) tokens billed by this destination.')}
                </FieldDescription>
              </div>
              <div className="grid gap-2">
                <Label htmlFor={`outputPrice-${index}`}>{t('Output price (USD / 1M tokens)')}</Label>
                <Input
                  id={`outputPrice-${index}`}
                  type="number"
                  step="0.0001"
                  min="0"
                  placeholder="0.00"
                  {...form.register(`destination.${index}.outputTokenPricePer1M`)}
                />
                <FieldDescription>
                  {t('Cost per 1,000,000 output (completion) tokens billed by this destination.')}
                </FieldDescription>
              </div>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="patch">{t('Patch request')}</Label>
              <div className="flex flex-row gap-2">
                <Textarea
                  id="patch"
                  placeholder='{ "key": "value" }'
                  required
                  {...form.register(`destination.${index}.patch`)}
                />
              </div>
            </div>
          </div>
        </form>
      </CardContent>
    </Card>
  );
};

const SettingsPanel = ({ llm }: { llm: LlmItem; onEnvironmentVariableSet?: (name: string, value: string) => void }) => {
  const { t } = useTranslation();

  const form = useForm<z.input<typeof formSchema>, unknown, z.output<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      names: llm.names ?? [''],
      destination: llm.destination ?? []
    },
    mode: 'onChange'
  });

  const { patchAsync, error, isFetching } = useUpdateLlm(llm.id ?? '');

  const names = form.watch('names');
  const destination = form.watch('destination');

  const {
    fields: destinationFields,
    append: appendDestination,
    remove: removeDestination
  } = useFieldArray({
    control: form.control,
    name: 'destination'
  });

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('LLM configuration settings')}</h3>
        </div>
        <div className="flex flex-row items-center">
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              patchAsync({
                names,
                destination
              }).then((response) => {
                if (!response.error) {
                  toast.success('LLM settings have been updated successfully.');
                }
              });
            }}>
            {isFetching ? <Spinner /> : <Save />} {t('Save Changes')}
          </Button>
        </div>
      </div>
      {error && (
        <div className="p-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error updating LLM settings')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col gap-4 p-2 h-full overflow-auto">
        <Form {...form}>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              e.stopPropagation();
            }}>
            <div className="flex flex-col gap-4 p-4 pt-0">
              {names.map((value, index) => (
                <FormField
                  key={`name-${index}`}
                  control={form.control}
                  name={`names.${index}`}
                  render={() => (
                    <FormItem>
                      <FormLabel>{index == 0 ? t('Name') : t('Alias')}</FormLabel>
                      <div className="flex flex-row gap-2">
                        <Input
                          className="flex-1"
                          value={value}
                          onChange={(e) => {
                            form.setValue(`names.${index}`, e.target.value);
                          }}
                        />
                        <Button
                          variant="ghost"
                          onClick={() => {
                            form.setValue(
                              'names',
                              names.filter((_, i) => i !== index)
                            );
                          }}
                          disabled={names.length === 1}>
                          <Trash2 />
                        </Button>
                      </div>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ))}

              <div className="flex flex-row justify-start">
                <Button
                  variant="ghost"
                  onClick={() => {
                    form.setValue('names', [...names, '']);
                  }}>
                  <Plus /> {t('Add name alias')}
                </Button>
              </div>
              <FieldGroup>
                <FieldSet>
                  <FieldLegend>{t('Connections')}</FieldLegend>
                  <FieldDescription>
                    {t(
                      'Set up the connection details for this LLM, including OpenAI, Azure, or a self-hosted deployment.'
                    )}
                  </FieldDescription>
                  <FieldGroup>
                    {destinationFields.map((field, index) => (
                      <DestinationForm
                        key={field.id}
                        id={llm.id}
                        form={form}
                        index={index}
                        remove={removeDestination}
                      />
                    ))}
                    <div className="flex flex-row justify-start">
                      <Button
                        variant="ghost"
                        onClick={() => {
                          appendDestination({ uri: '', model: '', apiKey: '' });
                        }}>
                        <Plus /> {t('New LLM connection')}
                      </Button>
                    </div>
                  </FieldGroup>
                </FieldSet>
              </FieldGroup>
              <FieldGroup className="flex flex-row gap-2">
                <Field>
                  <FieldLabel>{t('Created at')}</FieldLabel>
                  <Input readOnly value={formatDateTime(llm?.createdAt ?? '')} />
                </Field>
                <Field>
                  <FieldLabel>{t('Created by')}</FieldLabel>
                  <Input readOnly value={llm?.createdBy?.displayName ?? llm?.createdBy?.name} />
                </Field>
                <Field>
                  <FieldLabel>{t('Modified at')}</FieldLabel>
                  <Input readOnly value={formatDateTime(llm?.modifiedAt ?? '')} />
                </Field>
                <Field>
                  <FieldLabel>{t('Modified by')}</FieldLabel>
                  <Input readOnly value={llm?.modifiedBy?.displayName ?? llm?.modifiedBy?.name} />
                </Field>
              </FieldGroup>
            </div>
          </form>
        </Form>
      </div>
    </div>
  );
};

export default SettingsPanel;
