import type { QueryResult } from '@/hooks/data/core';
import { useGetLlms, type LlmItem, type LlmSummary } from '@/hooks/data/llms';
import { createContext, useContext, useEffect, type ReactNode } from 'react';

type LlmServiceContextType = {
  isFetching: boolean;
  error: string | undefined;
  data?: QueryResult<LlmSummary>;
  createLlm?: (item: LlmItem) => Promise<void>;
  reload: (queryString?: string) => void;
};

const LlmServiceContext = createContext<LlmServiceContextType | undefined>(undefined);

export const useLlmService = () => {
  const ctx = useContext(LlmServiceContext);
  if (!ctx) throw new Error('useLlmService must be used within <LlmServiceContext>');
  return ctx;
};

export const LlmServiceProvider = ({ children }: { children: ReactNode }) => {
  const { fetchAsync, data, error, isFetching } = useGetLlms();

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  return (
    <LlmServiceContext.Provider
      value={{
        reload: (queryString?: string) => fetchAsync(queryString ?? ''),
        data,
        error,
        isFetching
      }}>
      {children}
    </LlmServiceContext.Provider>
  );
};
