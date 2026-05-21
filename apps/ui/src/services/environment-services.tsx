import type { QueryResult } from '@/hooks/data/core';
import { useGetEnvironments, type EnvironmentItem, type EnvironmentSummary } from '@/hooks/data/environments';
import { createContext, useContext, useEffect, type ReactNode } from 'react';

type EnvironmentServiceContextType = {
  isFetching: boolean;
  error: string | undefined;
  data?: QueryResult<EnvironmentSummary>;
  createEnvironment?: (item: EnvironmentItem) => Promise<void>;
  reload: (queryString?: string) => void;
};

const EnvironmentServiceContext = createContext<EnvironmentServiceContextType | undefined>(undefined);

export const useEnvironmentService = () => {
  const ctx = useContext(EnvironmentServiceContext);
  if (!ctx) throw new Error('useEnvironmentService must be used within <EnvironmentServiceContext>');
  return ctx;
};

export const EnvironmentServiceProvider = ({ children }: { children: ReactNode }) => {
  const { fetchAsync, data, error, isFetching } = useGetEnvironments();

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  return (
    <EnvironmentServiceContext.Provider
      value={{
        reload: (queryString) => fetchAsync(queryString ?? ''),
        data,
        error,
        isFetching
      }}>
      {children}
    </EnvironmentServiceContext.Provider>
  );
};
