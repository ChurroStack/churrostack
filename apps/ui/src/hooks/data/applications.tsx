import { useDelete, useGet, usePatch, usePost, type QueryResult, type UseGetResult } from './core';
import type { MemberSummary } from './identities';
import type { TemplateItem } from './templates';

export type ApplicationMode = 'application' | 'workspace';

export interface ApplicationSummary {
  name: string;
  environmentName?: string;
  provisionStatus: DeploymentProvisionStatus;
  executionStatus: DeploymentExecutionStatus;
  mode: ApplicationMode;
  metrics?: {
    cpu_usage?: number;
    memory_usage?: number;
    cpu_usage_pct?: number;
    memory_usage_pct?: number;
    gpu_usage_pct?: number;
    storage_usage_pct?: number;
    cpu_limit?: number;
    memory_limit?: number;
    gpu_limit?: number;
    storage_limit?: number;
  };
  tags: string[];
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
export interface DeploymentCondition {
  timestamp: string;
  reason: string;
  message: string;
  type: string;
}
export interface DeploymentStatus {
  replicas: number;
  available: number;
  conditions: DeploymentCondition[];
}

export interface PortDefinition {
  name: string;
  title?: string;
  port?: number;
  icon?: string;
  description?: string;
  uri?: string;
  protocol?: 'generic' | 'web' | 'api' | 'mcp' | 'oai';
  sharing?: 'none' | 'members';
  authentication?: 'anonymous' | 'jwt' | 'jwt_dcr' | 'oidc';
  members?: MemberSummary[];
}

export interface ApplicationEnvironmentVariable {
  name: string;
  value: string;
}

export interface ApplicationSize {
  cpu?: string;
  memory?: string;
  gpu?: string;
  storage?: string;
  hint?: string;
}

export interface ApplicationExtensionItem {
  name: string;
  enabled: boolean;
  template: string;
  parameters?: { [key: string]: string[] };
}

export interface ApplicationItem {
  name: string;
  mode: ApplicationMode;
  members: MemberSummary[];
  extensions: ApplicationExtensionItem[];
  deployments: ApplicationDeploymentItem[];
  environmentName: string;
  size?: ApplicationSize;
  variables: ApplicationEnvironmentVariable[];
  ports: PortDefinition[];
  template: TemplateItem;
  parameters: { [name: string]: string[] };
  metadata: any;
  tags: string[];
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

export interface ApplicationEventItem {
  timestamp: string;
  target: string;
  type: string;
  reason: string;
  message: string;
  tags?: { [key: string]: string };
}

export interface ApplicationDeploymentItem {
  name: string;
  metrics?: {
    cpu_usage?: number;
    memory_usage?: number;
  };
  owner: {
    name: string;
    displayName: string;
  };
  provisionStatus: DeploymentProvisionStatus;
  executionStatus: DeploymentExecutionStatus;
  deploymentStatus: DeploymentStatus;
  deployedAt: string;
  metadata: any;
  tags: string[];
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

export interface NewApplicationItem {
  name: string;
}

export interface MetricValue {
  timestamp: string;
  value: number;
}

export interface MetricItem {
  name: string;
  values: MetricValue[];
}

export interface ApplicationUsageItem {
  identityName: string;
  applicationName: string;
  environmentName: string;
  from?: string;
  to?: string;
  requests: number;
  incomingTraffic: number;
  outgoingTraffic: number;
}

export interface ApplicationTraceItem {
  identityName?: string;
  applicationName: string;
  protocol?: string;
  method?: string;
  service?: string;
  host?: string;
  path?: string;
  statusCode?: string;
  isError: boolean;
  clientId?: string;
  requestBytes: number;
  responseBytes: number;
  duration: number;
  timestamp: string;
  tags: any;
}

export type ApplicationStatus = 'pending' | 'provisioning' | 'stopped' | 'failed' | 'starting' | 'running' | 'stopping';
export type DeploymentExecutionStatus = 'stopped' | 'starting' | 'running' | 'stopping';
export type DeploymentProvisionStatus = 'pending' | 'provisioning' | 'provisioned' | 'failed';

export interface DeploymentSummary {
  name: string;
  hash: string;
  template: string;
  createdOn: string;
}

export function getApplicationStatus(
  deploymentProvisionStatus: DeploymentProvisionStatus,
  deploymentExecutionStatus: DeploymentExecutionStatus
): ApplicationStatus {
  if (deploymentProvisionStatus === 'failed') {
    return 'failed';
  }
  if (deploymentProvisionStatus === 'pending') {
    return 'pending';
  }
  if (deploymentProvisionStatus === 'provisioning') {
    return 'provisioning';
  }
  if (deploymentProvisionStatus === 'provisioned') {
    if (deploymentExecutionStatus === 'starting') {
      return 'starting';
    }
    if (deploymentExecutionStatus === 'stopping') {
      return 'stopping';
    }
    if (deploymentExecutionStatus === 'running') {
      return 'running';
    }
    return 'stopped';
  }
  return 'pending';
}

export function useGetApplications(): UseGetResult<QueryResult<ApplicationSummary>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<ApplicationSummary>>(`/api/applications`);
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

export function useCreateApplication() {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<NewApplicationItem>(
    `/api/applications`,
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

export function useGetApplication(applicationName?: string): UseGetResult<ApplicationItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<ApplicationItem>(
    applicationName ? `/api/applications/${applicationName.replace(/^\/+/, '')}` : `/api/applications`
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

export function useUpdateApplication(path?: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, patchAsync, reset } = usePatch<ApplicationItem>(
    path ? `/api/applications/${path.replace(/^\/+/, '')}` : `/api/applications`,
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

export function useDeleteApplication() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(`/api/applications`);
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

export function useDeployApplication(applicationName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<DeploymentSummary>(
    `/api/applications/${applicationName}/deploy`,
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

export function useStartApplication(applicationName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<DeploymentSummary>(
    `/api/applications/${applicationName}/start`,
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

export function useStopApplication(applicationName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<DeploymentSummary>(
    `/api/applications/${applicationName}/stop`,
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

export function useGetApplicationEvents(appName: string): UseGetResult<QueryResult<ApplicationEventItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<
    QueryResult<ApplicationEventItem>
  >(`/api/applications/${appName}/events`);
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

export function useGetApplicationTraces(appName: string): UseGetResult<QueryResult<ApplicationTraceItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<
    QueryResult<ApplicationTraceItem>
  >(`/api/applications/${appName}/traces`);
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

export function useGetApplicationMetric(appName: string, metricName: string): UseGetResult<MetricItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<MetricItem>(
    `/api/applications/${appName}/metrics/${metricName}`
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

export function useGetApplicationUsage(
  appName: string,
  groupBy: string
): UseGetResult<QueryResult<ApplicationUsageItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<
    QueryResult<ApplicationUsageItem>
  >(`/api/applications/${appName}/usage/${groupBy}`);
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

export function useGetApplicationDeployments(
  applicationName?: string
): UseGetResult<QueryResult<ApplicationDeploymentItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<
    QueryResult<ApplicationDeploymentItem>
  >(`/api/applications/${applicationName?.replace(/^\/+/, '')}/deployments`);
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

export function useCreateApplicationDeployment(appName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } =
    usePost<ApplicationDeploymentItem>(`/api/applications/${appName}/deployments`, 'application/json');

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

export type SizeRecommendationDirection =
  | 'downsize'
  | 'upsize'
  | 'resize'
  | 'optimal'
  | 'insufficient_data'
  | 'not_analyzed';

export interface ApplicationSizeRecommendation {
  applicationName: string;
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
  computedAt: string;
  hasRecommendation: boolean;
  direction: SizeRecommendationDirection;
}

export interface AnalyzeUsageResult {
  applicationsAnalyzed: number;
  recommendationsCount: number;
}

export function useGetApplicationSizeRecommendation(
  applicationName?: string
): UseGetResult<ApplicationSizeRecommendation> {
  // useGet must always run (rules of hooks); when applicationName is missing we
  // point at a sentinel path and swap fetchAsync for a no-op so callers can't
  // accidentally hit the wrong-shape `/api/applications` list endpoint.
  const path = applicationName
    ? `/api/applications/${applicationName.replace(/^\/+/, '')}/size-recommendation`
    : `/api/applications/__unset__/size-recommendation`;
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<ApplicationSizeRecommendation>(path);
  const safeFetchAsync = applicationName
    ? fetchAsync
    : async () => ({ data: undefined, error: undefined });
  return { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync: safeFetchAsync, reset };
}

export function useAnalyzeApplicationUsage(applicationName: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<AnalyzeUsageResult>(
    `/api/applications/${applicationName}/analyze-usage`,
    'application/json'
  );
  return { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset };
}
