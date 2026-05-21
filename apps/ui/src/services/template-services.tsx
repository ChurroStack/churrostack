import type { QueryResult } from '@/hooks/data/core';
import { useGetTemplates, type TemplateItem, type TemplateSummary } from '@/hooks/data/templates';
import { createContext, useContext, useEffect, type ReactNode } from 'react';

type TemplateServiceContextType = {
  isFetching: boolean;
  error: string | undefined;
  data?: QueryResult<TemplateSummary>;
  createTemplate?: (item: TemplateItem) => Promise<void>;
  reload: (queryString?: string) => void;
};

const TemplateServiceContext = createContext<TemplateServiceContextType | undefined>(undefined);

export const useTemplateService = () => {
  const ctx = useContext(TemplateServiceContext);
  if (!ctx) throw new Error('useTemplateService must be used within <TemplateServiceContext>');
  return ctx;
};

export const TemplateServiceProvider = ({ children }: { children: ReactNode }) => {
  const { fetchAsync, data, error, isFetching } = useGetTemplates();

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  return (
    <TemplateServiceContext.Provider
      value={{
        reload: (queryString?: string) => fetchAsync(queryString ?? ''),
        data,
        error,
        isFetching
      }}>
      {children}
    </TemplateServiceContext.Provider>
  );
};
