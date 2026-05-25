import { AppWindowMac, Contact, ShieldUser, User, Users } from 'lucide-react';
import { type QueryResult, useDelete, useGet, type UseGetResult, usePost } from './core';
import { useProfile } from './profile';

export type IdentityType = 'user' | 'group' | 'application';
export type IdentityRole = 'user' | 'administrator';
export type IdentityPermissionLevel = 'owner' | 'contribute' | 'read' | 'execute' | 'none';
export type MembersMode = 'onlyInheritance' | 'onlyUniques' | 'mixed';
export type IdentityAccessMode = 'readOnly' | 'collaborate' | 'fullControl';

export class PermissionHelper {
  public static None = 0;
  public static Execute = 1 << 0; // 1
  public static Write = 1 << 1; // 2
  public static Read = 1 << 2; // 4
  public static Manage = 1 << 3; // 8
}

export interface IdentitySummary {
  name: string;
  displayName: string;
  type: IdentityType;
  permission: number;
  isInherited: boolean;
}

export interface IdentityItem {
  name: string;
  displayName: string;
  type: IdentityType;
  role: IdentityRole;
  accessMode: IdentityAccessMode;
  modifiedAt: string;
}

export interface MemberSummary {
  identity: IdentitySummary;
  permission: number;
}

export interface IdentityWithAssignedItem extends IdentityItem {
  assigned: string[];
  clientSecret?: string;
}

export interface UpdateIdentityMembersItem {
  override: boolean;
  members: UpdateIdentityMemberItem[];
}

export interface UpdateIdentityMemberItem {
  identityName: string;
  permission: number;
}

export interface AuthorizedServiceItem {
  id: string;
  name: string;
}

export function getPermissionLevel(permission: number): IdentityPermissionLevel {
  if (!permission) return 'none';

  if (permission & PermissionHelper.Manage) {
    return 'owner';
  } else if (permission & PermissionHelper.Write) {
    return 'contribute';
  } else if (permission & PermissionHelper.Read) {
    return 'read';
  } else if (permission & PermissionHelper.Execute) {
    return 'execute';
  } else {
    return 'none';
  }
}

export interface MyPermission {
  permission: number;
  isAdmin: boolean;
  canEdit: boolean;
  canManage: boolean;
}

/**
 * Resolves the current user's effective permission on an entity from its member
 * list, accounting for group memberships and the administrator role.
 */
export function useMyPermission(members?: MemberSummary[]): MyPermission {
  const { profile } = useProfile();
  const isAdmin = profile?.role === 'administrator';
  const identityNames = profile ? [profile.name, ...(profile.memberOf ?? [])] : [];

  let permission = PermissionHelper.None;
  for (const member of members ?? []) {
    if (member.identity && identityNames.includes(member.identity.name)) {
      permission |= member.permission;
    }
  }

  return {
    permission,
    isAdmin,
    canEdit: isAdmin || (permission & PermissionHelper.Write) !== 0,
    canManage: isAdmin || (permission & PermissionHelper.Manage) !== 0
  };
}

export function getPermission(permissionLevel: IdentityPermissionLevel): number {
  if (!permissionLevel) return PermissionHelper.None;

  switch (permissionLevel) {
    case 'owner':
      return PermissionHelper.Execute | PermissionHelper.Read | PermissionHelper.Write | PermissionHelper.Manage;
    case 'contribute':
      return PermissionHelper.Execute | PermissionHelper.Read | PermissionHelper.Write;
    case 'read':
      return PermissionHelper.Execute | PermissionHelper.Read;
    case 'execute':
      return PermissionHelper.Execute;
    default:
      return PermissionHelper.None;
  }
}

export const getUserIcon = (identity: IdentityItem | IdentitySummary, className?: string) => {
  return getIdentityTypeIcon(identity.type, className);
};

export const getIdentityTypeIcon = (type?: IdentityType, className?: string) => {
  if (!type) return '';

  switch (type.toLowerCase()) {
    case 'user':
      return <User className={className} />;
    case 'group':
      return <Users className={className} />;
    case 'application':
      return <AppWindowMac className={className} />;
    default:
      return '';
  }
};

export const getIdentityRoleIcon = (type?: IdentityRole, className?: string) => {
  if (!type) return '';

  switch (type.toLowerCase()) {
    case 'user':
      return <Contact className={className} />;
    case 'administrator':
      return <ShieldUser className={className} />;
    default:
      return '';
  }
};

export function useSearchIdentities(): UseGetResult<QueryResult<IdentityItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<IdentityItem>>(`/api/identities`);
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

export function useIdentity(identityName?: string): UseGetResult<IdentityWithAssignedItem> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<IdentityWithAssignedItem>(
      identityName ? `/api/identities/${identityName.replace(/^\/+/, '')}` : `/api/identities`
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

export function useUpsertIdentity(identityName?: string) {
  const { isFetching, isSuccess, statusCode, isError, error, data, postAsync, reset } =
    usePost<IdentityWithAssignedItem>(
      identityName ? `/api/identities/${identityName.replace(/^\/+/, '')}` : `/api/identities`,
      'application/json'
    );

  const jsonPostAsync = async (body: any, path?: string) => {
    return await postAsync(JSON.stringify(body), 'application/json', path);
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

export function useDeleteIdentity() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } = useDelete(`/api/identities`);
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

export function useAuthorizedServices(): UseGetResult<QueryResult<AuthorizedServiceItem>> {
  const { isFetching, isSuccess, statusCode, isError, error, data, fetchAsync, reset } =
    useGet<QueryResult<AuthorizedServiceItem>>(`/api/identities/authorizations`);
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

export function useDeleteAuthorizedService() {
  const { isFetching, isSuccess, statusCode, isError, error, deleteAsync, reset } =
    useDelete(`/api/identities/authorizations`);
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
