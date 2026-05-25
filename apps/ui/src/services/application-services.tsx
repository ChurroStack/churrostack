import type { QueryResult } from '@/hooks/data/core';
import { useGetApplications, type ApplicationItem, type ApplicationSummary } from '@/hooks/data/applications';
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

type ApplicationServiceContextType = {
  isFetching: boolean;
  error: string | undefined;
  data?: QueryResult<ApplicationSummary>;
  createApplication?: (item: ApplicationItem) => Promise<void>;
  reload: (queryString?: string) => void;
  currentApplication?: ApplicationItem;
  setCurrentApplication: (item: ApplicationItem | undefined) => void;
};

const ApplicationServiceContext = createContext<ApplicationServiceContextType | undefined>(undefined);

export const useApplicationService = () => {
  const ctx = useContext(ApplicationServiceContext);
  if (!ctx) throw new Error('useApplicationService must be used within <ApplicationServiceContext>');
  return ctx;
};

export const ApplicationServiceProvider = ({ children }: { children: ReactNode }) => {
  const { fetchAsync, data, error, isFetching } = useGetApplications();
  const [currentApplication, setCurrentApplication] = useState<ApplicationItem | undefined>(undefined);

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  return (
    <ApplicationServiceContext.Provider
      value={{
        reload: (queryString?: string) => fetchAsync(queryString ?? ''),
        data,
        error,
        isFetching,
        currentApplication,
        setCurrentApplication
      }}>
      {children}
    </ApplicationServiceContext.Provider>
  );
};
