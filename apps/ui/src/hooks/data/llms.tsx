import type { MetricItem } from './applications';
import { useDelete, useGet, usePatch, usePost, type QueryResult, type UseGetResult } from './core';
import type { MemberSummary } from './identities';

export interface LlmSummary {
  id: string;
  names: string[];
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

export interface LlmDestinationItem {
  uri: string;
  model: string;
  apiKey?: string;
  patch?: string;
  inputTokenPricePer1M?: number;
  outputTokenPricePer1M?: number;
}

export interface LlmItem {
  id: string;
  names: string[];
  destination: LlmDestinationItem[];
  members: MemberSummary[];
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

export interface NewLlmItem {
  names: string[];
}

export interface OaiModel {
  id: string;
}

export interface OaiModels {
  data: OaiModel[];
}

export interface LlmUsageSummaryItem {
  name: string;
  promptTokens: number;
  completionTokens: number;
  completions: number;
  inputSpend: number;
  outputSpend: number;
  totalSpend: number;
  peakRpm: number;
  peakTpm: number;
}

export function useGetLlms(): UseGetResult<QueryResult<LlmSummary>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<LlmSummary>>(`/api/llms`);
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

export function useCreateLlm() {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<LlmItem>(
    `/api/llms`,
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

export function useGetLlm(llmId: string): UseGetResult<LlmItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<LlmItem>(
    llmId ? `/api/llms/${llmId}` : `/api/llms`
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

export function useUpdateLlm(llmId: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, patchAsync, reset } = usePatch<LlmItem>(
    llmId ? `/api/llms/${llmId}` : `/api/llms`,
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

export function useTestLlmDestination(llmId: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<OaiModels>(
    `/api/llms/${llmId}/test`,
    'application/json'
  );

  const jsonPostAsync = async (uri: string, model: string, apiKey?: string) => {
    return await postAsync(JSON.stringify({ uri, model, apiKey }), 'application/json');
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

export function useGetLlmDestinationModels(llmId: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } = usePost<OaiModels>(
    `/api/llms/${llmId}/models`,
    'application/json'
  );

  const jsonPostAsync = async (uri: string, apiKey?: string) => {
    return await postAsync(JSON.stringify({ uri, apiKey }), 'application/json');
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

export function useDeleteLlm() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(`/api/llms`);
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

// llmId is optional: when omitted, the URL switches to the cross-LLM aggregated endpoint that
// the API scopes to whatever LLMs the caller has Read access on. Keeping ID handling as string
// avoids JS Number precision issues with IdGen snowflake longs.
export function useGetLlmMetric(llmId: string | undefined, metricName: string): UseGetResult<MetricItem> {
  const path = llmId ? `/api/llms/${llmId}/metrics/${metricName}` : `/api/llms/metrics/${metricName}`;
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<MetricItem>(path, {
    resetDataOnQueryChange: true
  });
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

export function useGetLlmUsage(llmId: string | undefined, groupBy: string): UseGetResult<QueryResult<LlmUsageSummaryItem>> {
  const path = llmId ? `/api/llms/${llmId}/usage/${groupBy}` : `/api/llms/usage/${groupBy}`;
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } = useGet<
    QueryResult<LlmUsageSummaryItem>
  >(path, { resetDataOnQueryChange: true });
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
