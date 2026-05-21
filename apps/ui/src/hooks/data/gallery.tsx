import { useGet, type QueryResult, type UseGetResult } from './core';

export interface GalleryAppSummary {
  name: string;
  icon: string;
  type: string;
  description: string;
  path: string;
}

export interface GalleryLlmSummary {
  id: string;
  names: string[];
  icon: string;
}

export function useGetGalleryApps(): UseGetResult<QueryResult<GalleryAppSummary>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<GalleryAppSummary>>(`/api/gallery/applications`);
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

export function useGetGalleryLlms(): UseGetResult<QueryResult<GalleryLlmSummary>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<GalleryLlmSummary>>(`/api/gallery/llms`);
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
