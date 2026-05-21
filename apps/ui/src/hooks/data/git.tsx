import { usePost } from './core';

export interface GetGitRepositoryInfo {
  url: string;
  branches: string[];
}

export function useGetGitRepositoryInfo() {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<GetGitRepositoryInfo>(
    `/api/git/check`,
    'application/json'
  );

  const jsonPostAsync = async (environmentName: string, url: string, username?: string, password?: string) => {
    return await postAsync(
      JSON.stringify({ environment: environmentName, url, username, password }),
      'application/json'
    );
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
