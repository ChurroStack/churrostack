import { useGet, usePost, usePut } from './core';

export interface EntraFilterRule {
  property: string;
  values: string[];
}

export interface EntraFilter {
  enabled: boolean;
  rules: EntraFilterRule[];
}

export interface EntraSyncConfig {
  enabled: boolean;
  /** Read-only: set by the operator (account entraTenantId, else ExternalProviders:Microsoft:TenantId); ignored when saving. */
  tenantId?: string;
  cronExpression: string;
  filter: EntraFilter;
}

export interface EntraSyncConfigItem {
  config: EntraSyncConfig;
  /** False when the Microsoft app registration (ExternalProviders:Microsoft) is not configured on the server. */
  available: boolean;
}

export type EntraSyncState = 'idle' | 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled';

export interface EntraSyncStatus {
  runId: string;
  state: EntraSyncState;
  phase?: string;
  processed: number;
  total?: number;
  trigger?: string;
  startedAt?: string;
  heartbeatAt?: string;
  finishedAt?: string;
  error?: string;
  summary?: string;
  warnings: string[];
}

export const isEntraSyncActive = (status?: EntraSyncStatus) =>
  status?.state === 'queued' || status?.state === 'running';

const basePath = '/api/identities/sync/entra';

export function useEntraSyncConfig() {
  return useGet<EntraSyncConfigItem>(`${basePath}/config`);
}

export function useSaveEntraSyncConfig() {
  const { isFetching, error, data, putAsync, reset } = usePut<EntraSyncConfigItem>(
    `${basePath}/config`,
    'application/json'
  );
  const jsonPutAsync = async (body: EntraSyncConfig) => await putAsync(JSON.stringify(body), 'application/json');
  return { isFetching, error, data, putAsync: jsonPutAsync, reset };
}

export function useEntraSyncStatus() {
  return useGet<EntraSyncStatus>(`${basePath}/status`);
}

export function useStartEntraSync() {
  const { isFetching, error, postAsync, reset } = usePost<EntraSyncStatus>(`${basePath}/start`);
  return { isFetching, error, startAsync: () => postAsync(), reset };
}

export function useStopEntraSync() {
  const { isFetching, error, postAsync, reset } = usePost<EntraSyncStatus>(`${basePath}/stop`);
  return { isFetching, error, stopAsync: () => postAsync(), reset };
}
