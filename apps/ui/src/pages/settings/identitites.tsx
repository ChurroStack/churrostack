import { DataTable } from '@/components/data-table';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ButtonGroup } from '@/components/ui/button-group';
import {
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { formatDateTime, formatDistanceToNow } from '@/extensions';
import {
  getIdentityRoleIcon,
  getIdentityTypeIcon,
  getUserIcon,
  useDeleteIdentity,
  useSearchIdentities,
  type IdentityItem,
  type IdentityRole,
  type IdentityType
} from '@/hooks/data/identities';
import { DropdownMenu } from '@radix-ui/react-dropdown-menu';
import { createColumnHelper, type ColumnDef } from '@tanstack/react-table';
import { AlertCircle, DownloadCloud, MoreVertical, Shield, UploadCloud, UserRoundSearch, Users } from 'lucide-react';
import type { QueryOptions } from 'odata-query';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { UpsertIdentityModal } from './dialogs/upsert-identity';
import { getOidc } from '@/oidc';
import { baseUrl } from '@/hooks/data/core';

export default function IdentitiesPage() {
  const { t } = useTranslation();
  const { isFetching, error, data, fetchAsync } = useSearchIdentities();
  const { isFetching: isDeleting, error: deleteError, deleteAsync } = useDeleteIdentity();
  const [query, setQuery] = useState<Partial<QueryOptions<any>>>({
    top: 10,
    skip: 0
  });
  const [typeFilter, setTypeFilter] = useState<IdentityType | undefined>(undefined);
  const [roleFilter, setRoleFilter] = useState<IdentityRole | undefined>(undefined);
  const [identitySelected, setIdentitySelected] = useState<IdentityItem | undefined>(undefined);
  const [showUpsertDialog, setShowUpsertDialog] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const queryString = `search=${query.search ?? ''}${query.top ? `&top=${query.top}` : ''}${
    query.skip ? `&skip=${query.skip}` : ''
  }${typeFilter ? `&type=${typeFilter}` : ''}${roleFilter ? `&role=${roleFilter}` : ''}`;

  const onSelectIdentity = (identity: IdentityItem) => {
    setIdentitySelected(identity);
    setShowUpsertDialog(true);
  };

  const onCreateClick = () => {
    setIdentitySelected(undefined);
    setShowUpsertDialog(true);
  };

  const onCancelUpsertDialog = () => {
    setShowUpsertDialog(false);
    setIdentitySelected(undefined);
  };

  const onSaveUpsertDialog = () => {
    setShowUpsertDialog(false);
    setIdentitySelected(undefined);
    reload();
  };

  const onDeleteSelection = (selectedItems: IdentityItem[]) => {
    if (!selectedItems || selectedItems.length === 0) return;

    return new Promise<void>((resolve, reject) => {
      const deletePromise = async () => {
        let success = true;
        for (const identity of selectedItems) {
          try {
            await deleteAsync(identity.name);
            success = true && success;
            //resetDelete();
          } catch (error) {
            success = false;
          }
        }
        reload();
        if (!success) throw 'Some items failed to be deleted';
      };
      toast.promise(deletePromise, {
        loading: t('Deleting...'),
        success: () => {
          resolve();
          return t('All items were deleted correctly');
        },
        error: () => {
          reject();
          return t('Some items failed to be deleted');
        }
      });
    });
  };

  const reload = (query?: string) => {
    fetchAsync(query ?? queryString);
  };

  useEffect(() => {
    reload();
  }, [query]);

  const columnHelper = createColumnHelper<IdentityItem>();
  const columns = [
    columnHelper.accessor('name', {
      id: 'name',
      header: () => t('Name'),
      cell: ({ row }) => (
        <Button
          variant="link"
          className="cursor-pointer p-0 has-[>svg]:px-0"
          onClick={() => onSelectIdentity(row.original)}>
          {getUserIcon(row.original)} {row.original.name}
        </Button>
      )
    }),
    columnHelper.accessor('displayName', {
      id: 'displayName',
      header: () => t('Display name'),
      cell: (user) => user.getValue()
    }),
    columnHelper.accessor('role', {
      header: () => t('Role'),
      cell: (user) => user.getValue()
    }),
    columnHelper.accessor('type', {
      header: () => t('Type'),
      cell: (user) => user.getValue()
    }),
    columnHelper.accessor('modifiedAt', {
      header: () => t('Modified on'),
      cell: ({ row }) => (
        <span>
          <Tooltip>
            <TooltipTrigger>{formatDistanceToNow(row.original.modifiedAt)}</TooltipTrigger>
            <TooltipContent side="bottom">{formatDateTime(row.original.modifiedAt)}</TooltipContent>
          </Tooltip>
        </span>
      )
    })
  ] as ColumnDef<IdentityItem>[];

  const onDownloadImportTemplate = async () => {
    const oidc = await getOidc();
    const accessToken = await oidc.getAccessToken();
    const response = await fetch(`${baseUrl}api/import/identities`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${accessToken}`
      }
    });

    if (!response.ok) {
      throw new Error('Failed to download file');
    }

    const blob = await response.blob();

    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;

    // optional filename
    a.download = 'identities_import_template.xlsx';

    document.body.appendChild(a);
    a.click();

    a.remove();
    window.URL.revokeObjectURL(url);
  };

  const onImportIdentities = async (file: File) => {
    await toast.promise<void>(
      async () => {
        const formData = new FormData();
        formData.append('file', file);

        const oidc = await getOidc();
        const accessToken = await oidc.getAccessToken();

        const result = await fetch(`${baseUrl}api/import/identities`, {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${accessToken}`
          },
          body: formData
        });

        if (!result.ok) {
          throw new Error('Failed to import identities');
        }

        reload();
      },
      {
        loading: 'Importing identities...',
        success: () => 'Identities imported successfully',
        error: 'Error importing identities'
      }
    );
  };

  const toolbar = [
    <ButtonGroup>
      <Button
        variant="outline"
        onClick={() => {
          fileInputRef.current?.click();
        }}>
        <UploadCloud /> {t('Import')}
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="icon" aria-label={t('More Options')}>
            <MoreVertical />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-90">
          <DropdownMenuGroup>
            <DropdownMenuItem onSelect={onDownloadImportTemplate}>
              <DownloadCloud />
              {t('Download import identities template')}
            </DropdownMenuItem>
          </DropdownMenuGroup>
        </DropdownMenuContent>
      </DropdownMenu>
    </ButtonGroup>,
    <DropdownMenu key="user-role-filter">
      <DropdownMenuTrigger>
        <Tooltip>
          <TooltipTrigger asChild>
            <Badge variant="secondary" className="h-8">
              {roleFilter ? (
                <>
                  {getIdentityRoleIcon(roleFilter)} {roleFilter}{' '}
                </>
              ) : (
                <>
                  <Shield /> {t('All roles')}
                </>
              )}
            </Badge>
          </TooltipTrigger>
          <TooltipContent side="bottom">{t('Filter by identity role')}</TooltipContent>
        </Tooltip>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel>{t('Identity roles')}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => setRoleFilter(undefined)}>
          <>
            <Shield /> {t('All roles')}
          </>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setRoleFilter('user')}>
          <>
            {getIdentityRoleIcon('user')} {t('User')}
          </>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setRoleFilter('administrator')}>
          <>
            {getIdentityRoleIcon('administrator')} {t('Administrator')}
          </>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>,
    <DropdownMenu key="user-type-filter">
      <DropdownMenuTrigger>
        <Tooltip>
          <TooltipTrigger asChild>
            <Badge variant="secondary" className="h-8">
              {typeFilter ? (
                <>
                  {getIdentityTypeIcon(typeFilter)} {typeFilter}{' '}
                </>
              ) : (
                <>
                  <UserRoundSearch /> {t('All types')}
                </>
              )}
            </Badge>
          </TooltipTrigger>
          <TooltipContent side="bottom">{t('Filter by identity type')}</TooltipContent>
        </Tooltip>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel>{t('Identity types')}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => setTypeFilter(undefined)}>
          <>
            <UserRoundSearch /> {t('All types')}
          </>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTypeFilter('application')}>
          <>
            {getIdentityTypeIcon('application')} {t('Application')}
          </>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTypeFilter('group')}>
          <>
            {getIdentityTypeIcon('group')} {t('Group')}
          </>
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => setTypeFilter('user')}>
          <>
            {getIdentityTypeIcon('user')} {t('User')}
          </>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  ];

  return (
    <div className="flex flex-1 flex-col gap-2 xl:space-y-4 bg-simple-card-glass px-4 py-2">
      {(error || deleteError) && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error loading identities')}</AlertTitle>
          <AlertDescription>{error || deleteError}</AlertDescription>
        </Alert>
      )}
      <input
        type="file"
        ref={fileInputRef}
        style={{ display: 'none' }}
        accept=".xlsx"
        onChange={(e) => {
          if (e.target.files && e.target.files.length > 0) {
            onImportIdentities(e.target.files[0]);
          }
        }}
      />
      <DataTable
        className="overflow-y-auto"
        data={data}
        columns={columns}
        onQueryChanged={setQuery}
        reload={reload}
        loading={isFetching || isDeleting}
        enableSelection
        onCreateClick={onCreateClick}
        onDeleteSelection={onDeleteSelection}
        leftOptions={[
          <div key="identities-header">
            <h3 className="text-lg font-medium flex flex-row gap-2 items-center">
              <Users className="size-4" />
              {t('Identities')}
            </h3>
            <p className="text-sm text-muted-foreground">
              {t(
                "Here you can manage the application's users and groups, create new ones, edit them, and delete them."
              )}
            </p>
          </div>
        ]}
        rightOptions={toolbar}
      />
      {showUpsertDialog && (
        <UpsertIdentityModal
          onClose={onCancelUpsertDialog}
          onSave={onSaveUpsertDialog}
          showDialog={showUpsertDialog}
          identity={identitySelected}
        />
      )}
    </div>
  );
}
