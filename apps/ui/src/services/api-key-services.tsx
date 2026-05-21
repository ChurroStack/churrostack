import type { QueryResult } from '@/hooks/data/core';
import { useGetApiKeys, type ApiKeyItem } from '@/hooks/data/api-keys';
import { createContext, useContext, useEffect, type ReactNode } from 'react';

type ApiKeyServiceContextType = {
  isFetching: boolean;
  error: string | undefined;
  data?: QueryResult<ApiKeyItem>;
  createApiKey?: (item: ApiKeyItem) => Promise<void>;
  reload: (queryString?: string) => void;
};

const ApiKeyServiceContext = createContext<ApiKeyServiceContextType | undefined>(undefined);

export const useApiKeyService = () => {
  const ctx = useContext(ApiKeyServiceContext);
  if (!ctx) throw new Error('useApiKeyService must be used within <ApiKeyServiceContext>');
  return ctx;
};

export const ApiKeyServiceProvider = ({ children }: { children: ReactNode }) => {
  const { fetchAsync, data, error, isFetching } = useGetApiKeys();

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  return (
    <ApiKeyServiceContext.Provider
      value={{
        reload: (queryString?: string) => fetchAsync(queryString ?? ''),
        data,
        error,
        isFetching
      }}>
      {children}
    </ApiKeyServiceContext.Provider>
  );
};
