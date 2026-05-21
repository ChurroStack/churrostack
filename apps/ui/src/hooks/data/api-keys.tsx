import { useDelete, useGet, usePost, type QueryResult, type UseGetResult } from './core';

export interface ApiKeyItem {
  id: string;
  description: string;
  identity: {
    name: string;
    displayName: string;
  };
  expiresAt: string;
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

export interface NewApiKeyItem {
  id: string;
  expiresAt: string;
  apiKey: string;
}

export function useGetApiKeys(): UseGetResult<QueryResult<ApiKeyItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<ApiKeyItem>>(`/api/keys`);
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

export function useGetKey(id?: string): UseGetResult<ApiKeyItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<ApiKeyItem>(
    id ? `/api/keys/${id.replace(/^\/+/, '')}` : `/api/keys`
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

export function useCreateApiKey() {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<NewApiKeyItem>(
    `/api/keys`,
    'application/json'
  );

  const jsonPostAsync = async (body: any, path?: string, queryString?: string, headers?: { [key: string]: string }) => {
    return await postAsync(JSON.stringify(body), 'application/json', path, queryString, headers);
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

export function useDeleteApiKey() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(`/api/keys`);
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
