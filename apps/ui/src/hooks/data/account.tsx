import { useGet } from './core';

export interface AccountSummary {
  name: string;
  owners: string[];
  quotas: QuotaItem[];
}

export interface QuotaItem {
  name: string;
  used: number;
  limit: number;
}

export function useGetAccount() {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<AccountSummary>(`/api/account`);
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
