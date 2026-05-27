import { useGet, type UseGetResult } from './core';

export type TagEntityType = 'applications' | 'environments';
export type TagPermission = 'read' | 'execute';

export function useGetTags(entityType: TagEntityType): UseGetResult<string[]> {
  return useGet<string[]>(`/api/tags/${entityType}`);
}
