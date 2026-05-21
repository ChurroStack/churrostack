import { useDelete, useGet, usePost, type QueryResult, type UseGetResult } from './core';

export interface HttpRequestItem {
  method: string;
  path: string;
  headers: { key: string; value: string }[];
  body?: string;
}

export interface ApplicationScheduleItem {
  name: string;
  enabled: boolean;
  description?: string;
  cronExpression: string;
  httpRequest: HttpRequestItem;
  createdAt?: string;
  createdBy?: {
    name: string;
    displayName: string;
  };
  modifiedAt?: string;
  modifiedBy?: {
    name: string;
    displayName: string;
  };
}

export function useGetApplicationSchedules(appName: string): UseGetResult<QueryResult<ApplicationScheduleItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<
    QueryResult<ApplicationScheduleItem>
  >(`/api/applications/${appName}/schedules`);
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

export function useUpsertApplicationSchedule(appName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } =
    usePost<ApplicationScheduleItem>(`/api/applications/${appName}/schedules`, 'application/json');

  const jsonPostAsync = async (
    body: ApplicationScheduleItem,
    path?: string,
    queryString?: string,
    headers?: { [key: string]: string }
  ) => {
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

export function useDeleteApplicationSchedules(appName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(
    `/api/applications/${appName}/schedules`
  );
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
