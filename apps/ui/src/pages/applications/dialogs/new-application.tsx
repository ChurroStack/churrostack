import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertCircle, AppWindow, Layers } from 'lucide-react';
import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema';
import { useFieldArray, useForm } from 'react-hook-form';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Spinner } from '@/components/ui/spinner';
import { useCreateApplication, type ApplicationExtensionItem } from '@/hooks/data/applications';
import { useNavigate } from 'react-router';
import TemplatePicker from '@/pickers/template-picker';
import EnvironmentPicker from '@/pickers/environment-picker';
import { Switch } from '@/components/ui/switch';
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldGroup,
  FieldLabel,
  FieldSet,
  FieldTitle
} from '@/components/ui/field';
import { useGetGitRepositoryInfo } from '@/hooks/data/git';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue
} from '@/components/ui/select';
import { useGetTemplate } from '@/hooks/data/templates';
import { Textarea } from '@/components/ui/textarea';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';

// The component props you want to require
export interface NewApplicationDialogProps {
  environmentName?: string;
  showDialog: boolean;
  reload?: () => void;
  onClose: () => void;
}

export interface WizardStepPage {
  validate: () => Promise<boolean>;
}

const formSchema = z.object({
  name: z
    .string()
    .max(254, 'Application name cannot have more than 254 characters')
    .min(1, 'Application name is required'),
  description: z.string().max(1024, 'Application description cannot have more than 1024 characters').optional(),
  mode: z.string(),
  template: z
    .string()
    .max(254, 'Application template cannot have more than 254 characters')
    .min(1, 'Application template is required'),
  environment: z
    .string()
    .max(254, 'Application environment cannot have more than 254 characters')
    .min(1, 'Application environment is required'),
  variables: z
    .array(
      z.object({
        name: z.string().min(1, 'Variable name is required'),
        value: z.string().min(1, 'Variable value is required')
      })
    )
    .optional(),
  extensions: z
    .array(
      z.object({
        name: z.string(),
        enabled: z.boolean(),
        template: z.string().min(1, 'Template name is required'),
        parameters: z.object().optional()
      })
    )
    .optional()
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
export function useNewApplicationDialogModel({ reload, onClose, environmentName }: NewApplicationDialogProps) {
  //const { reload: reloadApplications } = useApplicationService();
  const { isFetching, isSuccess, isError, error, postAsync, reset } = useCreateApplication();
  const navigate = useNavigate();

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: standardSchemaResolver(formSchema),
    defaultValues: {
      name: '',
      template: '',
      mode: 'application',
      environment: environmentName || '',
      extensions: [],
      variables: []
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

  async function onSubmit() {
    const values = form.getValues();
    const result = await postAsync({
      name: values.name,
      template: values.template,
      environment: values.environment,
      extensions: values.extensions,
      mode: values.mode,
      variables: values.variables,
      metadata: {
        description: values.description
      }
    });
    if (!result.error) {
      navigate(`/applications/${values.name}`);
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

export interface GeneralPageProps {
  form: ReturnType<typeof useForm<z.infer<typeof formSchema>>>;
  environmentName?: string;
}

const GeneralPage = forwardRef<WizardStepPage, GeneralPageProps>(({ form, environmentName }, ref) => {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setTimeout(() => {
      inputRef.current?.focus();
    }, 100);
  }, []);

  useImperativeHandle(ref, () => ({
    validate: async () => {
      const result = await form.trigger(['name', 'template', 'environment']);
      return result;
    }
  }));

  return (
    <>
      <FormField
        control={form.control}
        name="environment"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('Environment')}</FormLabel>
            <FormDescription>{t('Select the environment where you want to host this application')}</FormDescription>
            <FormControl>
              <div className="flex flex-row items-center">
                <EnvironmentPicker
                  value={field.value}
                  onChange={field.onChange}
                  autoSelect
                  readonly={!!environmentName}
                  className="w-full"
                />
              </div>
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="template"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('Template')}</FormLabel>
            <FormDescription>
              {t('Choose a template that matches the type of application you plan to publish.')}
            </FormDescription>
            <FormControl>
              <div className="flex flex-row items-center">
                <TemplatePicker value={field.value} onChange={field.onChange} className="w-full" />
              </div>
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      <FormField
        control={form.control}
        name="name"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('Application name')}</FormLabel>
            <FormDescription>{t('Write a unique DNS compatible name for your application')}</FormDescription>
            <FormControl>
              <div className="flex flex-row items-center">
                <Input
                  id="name"
                  placeholder={t('Enter the application name...')}
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
      <FormField
        control={form.control}
        name="description"
        render={({ field }) => (
          <FormItem>
            <FormLabel>{t('Description')}</FormLabel>
            <FormDescription>{t('End users will see this description in the application gallery.')}</FormDescription>
            <FormControl>
              <div className="flex flex-row items-center">
                <Textarea id="description" placeholder={t('Brief description about your application...')} {...field} />
              </div>
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
    </>
  );
});

export interface AppModePageProps {
  form: ReturnType<typeof useForm<z.infer<typeof formSchema>>>;
}

const AppModePage = forwardRef<WizardStepPage, AppModePageProps>(({ form }, ref) => {
  const { t } = useTranslation();

  useImperativeHandle(ref, () => ({
    validate: async () => true
  }));

  const mode = form.watch('mode');

  return (
    <FieldSet className="w-full">
      <FieldGroup>
        <FieldSet>
          <FieldDescription>
            {t(
              'Choose how the application is deployed across users—either as a shared instance or as isolated, per-user environments—affecting scalability, resource usage, and supported interactions.'
            )}
          </FieldDescription>
          <RadioGroup
            defaultValue="application"
            className="mt-4"
            value={mode}
            onValueChange={(value) => form.setValue('mode', value)}>
            <FieldLabel htmlFor="application">
              <Field orientation="horizontal">
                <FieldContent>
                  <FieldTitle className="flex flex-row gap-2">
                    <AppWindow /> {t('Application mode')}
                  </FieldTitle>
                  <FieldDescription>
                    {t(
                      'Runs a single, shared instance of the application that serves all users simultaneously. This is the standard deployment model for most web applications, enabling efficient resource utilization, centralized updates, and consistent behavior across users. Changes are deployed once and immediately available to everyone, simplifying maintenance and scaling.'
                    )}
                  </FieldDescription>
                </FieldContent>
                <RadioGroupItem value="application" id="application" aria-label="Application" />
              </Field>
            </FieldLabel>
            <FieldLabel htmlFor="workspace">
              <Field orientation="horizontal">
                <FieldContent>
                  <FieldTitle className="flex flex-row gap-2">
                    {' '}
                    <Layers /> {t('Workspace mode')}
                  </FieldTitle>
                  <FieldDescription>
                    {t(
                      'Deploys a dedicated, isolated instance for each user. Every user provisions and manages their own resources—such as memory, CPU, and storage—allowing them to operate fully independently without impacting others. This mode is especially well-suited for GUI-based applications where users need direct access to a desktop environment, for example to interact with captchas or complex UI workflows. Because each active user runs a separate instance, overall resource consumption scales linearly with the number of concurrent users.'
                    )}
                  </FieldDescription>
                </FieldContent>
                <RadioGroupItem value="workspace" id="workspace" aria-label="Workspace" />
              </Field>
            </FieldLabel>
          </RadioGroup>
        </FieldSet>
      </FieldGroup>
    </FieldSet>
  );
});

export interface GitPageProps {
  form: ReturnType<typeof useForm<z.infer<typeof formSchema>>>;
  environmentName?: string;
}

const GitPage = forwardRef<WizardStepPage, GitPageProps>(({ form, environmentName }, ref) => {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const [useGit, setUseGit] = useState<boolean>(true);
  const [useGitAuth, setUseGitAuth] = useState<boolean>(false);
  const [branches, setBranches] = useState<string[]>([]);

  //const variables = form.watch('variables') ?? [];
  //const gitExtension = form.watch('extensions')?.find((o) => o.template === 'com.churrostack.extension.git-sync') as ApplicationExtensionItem ?? {} as ApplicationExtensionItem;
  //const [selectedBranch, setSelectedBranch] = useState<string>(gitExtension?.parameters?.branch && gitExtension?.parameters?.branch.length > 0 ? gitExtension?.parameters?.branch[0] : 'main');
  const extensionIndex = form
    .getValues('extensions')
    ?.findIndex((o) => o.template === 'com.churrostack.extension.git-sync');
  const extension = form.watch(`extensions.${extensionIndex!}`) as ApplicationExtensionItem | undefined;

  const {
    fields: extensions,
    append: addExtension,
    remove: removeExtension
  } = useFieldArray({
    control: form.control,
    name: 'extensions'
  });

  const { postAsync, isFetching, error } = useGetGitRepositoryInfo();

  useEffect(() => {
    setTimeout(() => {
      inputRef.current?.focus();
    }, 100);
  }, []);

  useEffect(() => {
    if (!useGit) {
      const idx = extensions.findIndex((o) => o.template === 'com.churrostack.extension.git-sync');
      if (idx === -1) return;
      removeExtension(idx);
    } else {
      const idx = extensions.findIndex((o) => o.template === 'com.churrostack.extension.git-sync');
      if (idx !== -1) return;
      addExtension({ name: 'git-sync', enabled: true, template: 'com.churrostack.extension.git-sync', parameters: {} });
    }
  }, [useGit]);

  useEffect(() => {
    if (!useGitAuth) {
      const idx = extensions.findIndex((o) => o.template === 'com.churrostack.extension.git-sync');
      if (idx === -1) return;
      const parameters = extension?.parameters || {};
      delete parameters.username;
      delete parameters.password;
      form.setValue(`extensions.${extensionIndex!}.parameters`, parameters as any);
    }
  }, [useGitAuth]);

  const getGetRepositoryInfo = async () => {
    const gitUrl =
      extension?.parameters?.url && extension?.parameters?.url.length > 0 ? extension?.parameters?.url[0] : '';
    const gitUsername =
      extension?.parameters?.username && extension?.parameters?.username.length > 0
        ? extension?.parameters?.username[0]
        : '';
    const gitPassword =
      extension?.parameters?.password && extension?.parameters?.password.length > 0
        ? extension?.parameters?.password[0]
        : '';
    if (gitUrl && gitUrl.length > 0) {
      const gitInfo = await postAsync(environmentName ?? '', gitUrl, gitUsername, gitPassword);
      if (gitInfo.error) {
        form.setError('variables', {
          type: 'manual',
          message: t('Failed to fetch Git repository information: {{error}}', { error: gitInfo.error })
        });
        return;
      } else {
        setBranches(gitInfo.data?.branches ?? []);
        if (gitInfo.data?.branches?.length && gitInfo.data?.branches?.length > 0) {
          const parameters = extension?.parameters || {};
          parameters.branch = [gitInfo.data?.branches[0]];
          form.setValue(`extensions.${extensionIndex!}.parameters`, parameters as any);
        }
      }
    }
  };

  useImperativeHandle(ref, () => ({
    validate: async () => {
      if (!useGit) return true;
      const parameters = extension?.parameters || {};
      const gitInfo = await postAsync(
        environmentName ?? environmentName ?? '',
        parameters?.url && parameters.url.length > 0 ? parameters.url[0] : '',
        parameters?.username && parameters.username.length > 0 ? parameters.username[0] : '',
        parameters?.password && parameters.password.length > 0 ? parameters.password[0] : ''
      );
      return !gitInfo.error;
    }
  }));

  return (
    <>
      {error && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Cannot connect to git repository')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      <Field orientation="horizontal">
        <Switch id="use-git" checked={useGit} onCheckedChange={(checked) => setUseGit(checked)} />
        <FieldLabel htmlFor="use-git">{t('Deploy application from Git repository')}</FieldLabel>
      </Field>
      {useGit && extension && (
        <Field>
          <FieldLabel htmlFor="git-url">{t('Git Repository URL')}</FieldLabel>
          <Input
            id="git-url"
            placeholder={t('Enter the Git repository URL...')}
            required
            value={
              extension?.parameters?.url && extension?.parameters?.url.length > 0 ? extension?.parameters?.url[0] : ''
            }
            onChange={(e) =>
              form.setValue(`extensions.${extensionIndex!}.parameters`, {
                ...extension?.parameters,
                url: [e.target.value]
              } as any)
            }
          />
        </Field>
      )}
      {useGit && extension && (
        <Field orientation="horizontal">
          <Switch id="use-git-auth" checked={useGitAuth} onCheckedChange={(checked) => setUseGitAuth(checked)} />
          <FieldLabel htmlFor="use-git-auth">{t('My Git repository requires authentication')}</FieldLabel>
        </Field>
      )}
      {useGit && useGitAuth && extension && (
        <Field>
          <FieldLabel htmlFor="git-username">{t('Username')}</FieldLabel>
          <Input
            id="git-username"
            placeholder={t('Enter the Git username...')}
            required
            value={
              extension?.parameters?.username && extension?.parameters?.username.length > 0
                ? extension?.parameters?.username[0]
                : ''
            }
            onChange={(e) =>
              form.setValue(`extensions.${extensionIndex!}.parameters`, {
                ...extension?.parameters,
                username: [e.target.value]
              } as any)
            }
          />
        </Field>
      )}
      {useGit && useGitAuth && extension && (
        <Field>
          <FieldLabel htmlFor="git-password">{t('Password or PAT token')}</FieldLabel>
          <Input
            id="git-password"
            placeholder={t('Enter the Git password or PAT token...')}
            required
            value={
              extension?.parameters?.password && extension?.parameters?.password.length > 0
                ? extension?.parameters?.password[0]
                : ''
            }
            onChange={(e) =>
              form.setValue(`extensions.${extensionIndex!}.parameters`, {
                ...extension?.parameters,
                password: [e.target.value]
              } as any)
            }
          />
        </Field>
      )}
      {useGit && extension && (
        <Field>
          <FieldLabel htmlFor="git-branch">{t('Select the branch you want to clone')}</FieldLabel>
          <div className="flex flex-row items-center gap-2">
            <Select
              value={
                extension?.parameters?.branch && extension?.parameters?.branch.length > 0
                  ? extension?.parameters?.branch[0]
                  : undefined
              }
              onValueChange={(value) => {
                if (value && value != '')
                  form.setValue(`extensions.${extensionIndex!}.parameters`, {
                    ...extension?.parameters,
                    branch: [value]
                  } as any);
              }}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder={t('Select a branch')} />
              </SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectLabel>{t('Branches')}</SelectLabel>
                  {branches.map((branch) => (
                    <SelectItem key={branch} value={branch}>
                      {branch}
                    </SelectItem>
                  ))}
                </SelectGroup>
              </SelectContent>
            </Select>
            <Button
              onClick={(e) => {
                getGetRepositoryInfo();
                e.stopPropagation();
                e.preventDefault();
              }}>
              {isFetching && <Spinner />}
              {t('Fetch')}
            </Button>
          </div>
        </Field>
      )}
      {!useGit && (
        <Alert className="mt-4">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Git will not be used')}</AlertTitle>
          <AlertDescription>
            {t(
              'You have chosen not to deploy from a Git repository. After the application is created, upload the code manually to ensure it functions correctly.'
            )}
          </AlertDescription>
        </Alert>
      )}
    </>
  );
});

const PrevButtonText = ({ currentPage }: { currentPage: number }) => {
  const { t } = useTranslation();
  if (currentPage <= 1) {
    return t('Cancel');
  } else {
    return t('Previous');
  }
};

const NextButtonText = ({ currentPage, totalPages }: { currentPage: number; totalPages: number }) => {
  const { t } = useTranslation();
  if (currentPage < totalPages) {
    return t('Next');
  } else {
    return t('Create');
  }
};

// The pure view (V) in M-V-VM (no logic, no side effects)
export function NewApplicationDialogView({
  showDialog,
  isError,
  isFetching: isParentFetching,
  error,
  onOpenChange,
  form,
  onSubmit,
  environmentName
}: NewApplicationDialogProps & {
  onOpenChange: (open: boolean) => void;
  isError: boolean;
  isFetching: boolean;
  error?: string;
  onSubmit: (values: z.infer<typeof formSchema>) => Promise<void>;
  form: ReturnType<typeof useForm<z.infer<typeof formSchema>>>;
}) {
  const { t } = useTranslation();
  const [currentPage, setCurrentPage] = useState<number>(1);
  const template = form.watch('template');
  const environment = form.watch('environment');
  const stepRefs = useRef<Array<WizardStepPage | null>>([null, null, null]);
  const [templateUsesGit, setTemplateUsesGit] = useState<boolean>(false);
  const { fetchAsync: fetchTemplateAsync, isFetching: isTemplateFetching } = useGetTemplate(
    `${template}:${environment}`
  );

  const fetchTemplate = async () => {
    const result = await fetchTemplateAsync('');
    setTemplateUsesGit(
      !!result.data?.definition?.extensions.find((e) => e.template === 'com.churrostack.extension.git-sync')
    );
    form.setValue(
      'extensions',
      result.data?.definition?.extensions.map((ext) => ({
        name: ext.name,
        template: ext.template,
        enabled: ext.enabled ?? true,
        parameters: {}
      })) || []
    );
  };

  useEffect(() => {
    if (template && template != '' && environment && environment != '') {
      fetchTemplate();
    }
  }, [template, environment]);

  useEffect(() => {
    setCurrentPage(1);
    form.reset();
  }, [showDialog]);

  const onPrevious = () => {
    if (currentPage > 1) {
      setCurrentPage(currentPage - 1);
    } else {
      onOpenChange(false);
    }
  };

  const onNext = async () => {
    if (currentPage <= stepRefs.current?.length) {
      const stepRef = stepRefs.current[currentPage - 1];
      if (stepRef) {
        const isValid = await stepRef.validate();
        if (!isValid) return;
      }
      if (currentPage == (templateUsesGit ? stepRefs.current.length : stepRefs.current.length - 1)) {
        form.handleSubmit(onSubmit)();
      } else {
        setCurrentPage(currentPage + 1);
      }
    }
  };

  const isFetching = isParentFetching || isTemplateFetching;

  return (
    <AlertDialog open={showDialog} onOpenChange={onOpenChange}>
      <AlertDialogContent className="lg:min-w-220 min-h-150 min-w-full">
        <Form {...form}>
          <form
            onSubmit={(e) => {
              e.stopPropagation();
              e.preventDefault();
            }}>
            <div className="flex flex-col gap-4 p-4 lg:min-w-200 min-h-150">
              <AlertDialogHeader className="mb-4">
                <AlertDialogTitle>{t('New application')}</AlertDialogTitle>
                <AlertDialogDescription>{t('Creates a new application')}</AlertDialogDescription>
              </AlertDialogHeader>
              {isError && (
                <Alert className="mb-4" variant="destructive">
                  <AlertCircle className="size-4" />
                  <AlertTitle>{t('Error')}</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
              {currentPage == 1 && (
                <GeneralPage
                  form={form}
                  ref={(el) => {
                    if (stepRefs?.current) {
                      stepRefs.current[0] = el;
                    }
                  }}
                  environmentName={environmentName}
                />
              )}
              {currentPage == 2 && (
                <AppModePage
                  form={form}
                  ref={(el) => {
                    if (stepRefs?.current) {
                      stepRefs.current[1] = el;
                    }
                  }}
                />
              )}
              {currentPage == 3 && (
                <GitPage
                  form={form}
                  ref={(el) => {
                    if (stepRefs?.current) {
                      stepRefs.current[2] = el;
                    }
                  }}
                  environmentName={environment ?? environmentName}
                />
              )}
            </div>
            <AlertDialogFooter>
              <Button
                variant="secondary"
                disabled={isFetching}
                onClick={(e) => {
                  onPrevious();
                  e.stopPropagation();
                  e.preventDefault();
                }}>
                <PrevButtonText currentPage={currentPage} />
              </Button>
              <Button
                disabled={isFetching}
                onClick={(e) => {
                  onNext();
                  e.stopPropagation();
                  e.preventDefault();
                }}>
                {isFetching && <Spinner className="text-white-600 size-5" />}{' '}
                <NextButtonText currentPage={currentPage} totalPages={stepRefs.current?.length ?? 0} />
              </Button>
            </AlertDialogFooter>
          </form>
        </Form>
      </AlertDialogContent>
    </AlertDialog>
  );
}

// The ViewModel (VM) or final component in M-V-VM (composition of M and V)
export function NewApplicationDialogContent(props: NewApplicationDialogProps) {
  const model = useNewApplicationDialogModel(props);
  return <NewApplicationDialogView {...props} {...model} />;
}

export function NewApplicationDialog(props: NewApplicationDialogProps) {
  return <NewApplicationDialogContent {...props} />;
}
