import { useDelete, useGet, usePut, usePost, type QueryResult, type UseGetResult } from './core';
export interface TemplateSummary {
  name: string;
  target: string;
  icon: string;
  title: string;
  description: string;
  category: {
    icon: string;
    title: string;
    description: string;
  };
  type: string;
  translation: { [key: string]: string };
  createdAt: string;
  createdBy: {
    name: string;
    displayName: string;
  };
  modifiedAt: string;
  modifiedBy: {
    name: string;
    displayName: string;
  };
}

export type TemplateParameterType = 'string' | 'number' | 'boolean' | 'list';

export interface TemplateParameterDefinition {
  title?: string;
  type: TemplateParameterType;
  description?: string;
  ui_hint?: string;
  icon?: string;
  required?: boolean;
  multi?: boolean;
  condition?: string;
  default_value?: string[];
}

export interface TemplateExtensionItem {
  name: string;
  enabled: boolean;
  template: string;
}

export interface TemplateItem {
  name: string;
  target: string;
  icon: string;
  title: string;
  description: string;
  category: {
    icon: string;
    title: string;
    description: string;
  };
  type: string;
  definition: {
    icon: string;
    target: string;
    extensions: TemplateExtensionItem[];
    parameters: { [key: string]: TemplateParameterDefinition };
  };
  content: string;
  translation: { [key: string]: string };
  createdAt: string;
  createdBy: {
    name: string;
    displayName: string;
  };
  modifiedAt: string;
  modifiedBy: {
    name: string;
    displayName: string;
  };
}

export function useGetTemplates(): UseGetResult<QueryResult<TemplateSummary>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<TemplateSummary>>(`/api/templates`);
  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    fetchAsync,
    reset
  };
}

export function useCreateTemplate() {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<TemplateItem>(
    `/api/templates`,
    'application/json'
  );

  const jsonPostAsync = async (
    body: string,
    path?: string,
    queryString?: string,
    headers?: { [key: string]: string }
  ) => {
    return await postAsync(body, 'text/plain', path, queryString, headers);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    postAsync: jsonPostAsync,
    reset
  };
}

export function useGetTemplate(templateName?: string): UseGetResult<TemplateItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<TemplateItem>(
    templateName ? `/api/templates/${templateName.replace(/^\/+/, '')}` : `/api/templates`
  );
  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    fetchAsync,
    reset
  };
}

export function useUpdateTemplate(path?: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, putAsync, reset } = usePut<TemplateItem>(
    path ? `/api/templates/${path.replace(/^\/+/, '')}` : `/api/templates`,
    'application/json'
  );

  const plainPutAsync = async (body: string, path?: string, queryString?: string) => {
    return await putAsync(body, 'text/plain', path, queryString);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    putAsync: plainPutAsync,
    reset
  };
}

export function useDeleteTemplate() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(`/api/templates`);
  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    deleteAsync,
    reset
  };
}
