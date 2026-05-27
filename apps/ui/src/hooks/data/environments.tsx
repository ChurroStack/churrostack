import { useDelete, useGet, usePatch, usePost, type QueryResult, type UseGetResult } from './core';
import type { MemberSummary } from './identities';
import type {
  AnalyzeUsageResult,
  ApplicationSize,
  DeploymentExecutionStatus,
  DeploymentProvisionStatus,
  SizeRecommendationDirection
} from './applications';

export interface EnvironmentSummary {
  name: string;
  createdAt: string;
  createdBy: {
    name: string;
    displayName: string;
  };
  modifiedAt: string;
  modifiedBy: {
    name: string;
    displayName: string;
  };
}

export interface ResourceSize {
  cpu: string;
  memory: string;
  gpu: string;
  storage: string;
}

export interface EnvironmentSize {
  name: string;
  title: string;
  requests: ResourceSize;
  limits: ResourceSize;
}

export interface EnvironmentDefinition {
  sizes: EnvironmentSize[];
  capabilities: { [name: string]: string };
  limits: { [name: string]: string };
}

export interface EnvironmentHealthItem {
  healthy: boolean;
  timestamp: string;
  error?: string;
}

export interface EnvironmentItem {
  name: string;
  members: MemberSummary[];
  provisionStatus: string;
  definition: EnvironmentDefinition;
  health?: EnvironmentHealthItem;
  createdAt: string;
  createdBy: {
    name: string;
    displayName: string;
  };
  modifiedAt: string;
  modifiedBy: {
    name: string;
    displayName: string;
  };
}

export interface NewEnvironmentItem {
  name: string;
}

export interface EnvironmentKeysItem {
  sshPublicKey: string;
  sshPrivateKey: string;
  encryptionKey: string;
  host: string;
  port: number;
  knownHosts: string;
  namespace: string;
  valuesYaml: string;
}

export function useGetEnvironments(): UseGetResult<QueryResult<EnvironmentSummary>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<EnvironmentSummary>>(`/api/environments`);
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

export function useCreateEnvironment() {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<NewEnvironmentItem>(
    `/api/environments`,
    'application/json'
  );

  const jsonPostAsync = async (body: any, path?: string, queryString?: string, headers?: { [key: string]: string }) => {
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

export function useGetEnvironment(environmentName?: string): UseGetResult<EnvironmentItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<EnvironmentItem>(
    environmentName ? `/api/environments/${environmentName.replace(/^\/+/, '')}` : `/api/environments`
  );
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

export function useUpdateEnvironment(path?: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, patchAsync, reset } = usePatch<EnvironmentItem>(
    path ? `/api/environments/${path.replace(/^\/+/, '')}` : `/api/environments`,
    'application/json'
  );

  const jsonPatchAsync = async (body: any, path?: string, queryString?: string) => {
    return await patchAsync(JSON.stringify(body), 'application/json', path, queryString);
  };

  return {
    isFetching,
    isSuccess,
    statusCode,
    isError,
    error,
    data,
    patchAsync: jsonPatchAsync,
    reset
  };
}

export function useDeleteEnvironment() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(`/api/environments`);
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

export function useRotateEnvironmentKeys(environmentName?: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<EnvironmentKeysItem>(
    `/api/environments/${environmentName}/rotate`,
    'application/json'
  );

  const jsonPostAsync = async (body: any, path?: string, queryString?: string, headers?: { [key: string]: string }) => {
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

export interface EnvironmentUsageItem {
  applicationName: string;
  createdBy?: {
    name: string;
    displayName: string;
  };
  currentSize?: ApplicationSize;
  recommendedSize?: ApplicationSize;
  cpuAvg: number;
  cpuMax: number;
  cpuP95: number;
  memoryAvg: number;
  memoryMax: number;
  memoryP95: number;
  sampleCount: number;
  windowDays: number;
  computedAt?: string;
  hasRecommendation: boolean;
  direction: SizeRecommendationDirection;
  provisionStatus: DeploymentProvisionStatus;
  executionStatus: DeploymentExecutionStatus;
}

export function useGetEnvironmentUsage(environmentName?: string): UseGetResult<EnvironmentUsageItem[]> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<EnvironmentUsageItem[]>(
    environmentName ? `/api/environments/${environmentName.replace(/^\/+/, '')}/usage` : `/api/environments`
  );
  return { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset };
}

export function useAnalyzeEnvironmentUsage(environmentName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<AnalyzeUsageResult>(
    `/api/environments/${environmentName}/analyze-usage`,
    'application/json'
  );
  return { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset };
}

export interface ResourceTotal {
  used: number;
  requested: number;
  allocated: number;
  quota?: number;
}

export interface EnvironmentTotals {
  cpu: ResourceTotal;
  memory: ResourceTotal;
  gpu: ResourceTotal;
  storage: ResourceTotal;
}

export function useGetEnvironmentTotals(environmentName?: string): UseGetResult<EnvironmentTotals> {
  const path = environmentName
    ? `/api/environments/${environmentName.replace(/^\/+/, '')}/totals`
    : `/api/environments/__unset__/totals`;
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<EnvironmentTotals>(path);
  const safeFetchAsync = environmentName
    ? fetchAsync
    : async () => ({ data: undefined, error: undefined });
  return { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync: safeFetchAsync, reset };
}

export function useEnvironmentTest(environmentName?: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<EnvironmentKeysItem>(
    `/api/environments/${environmentName}/connect`,
    'application/json'
  );

  const jsonPostAsync = async (body: any, path?: string, queryString?: string, headers?: { [key: string]: string }) => {
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
