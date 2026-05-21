import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { BreadcrumbItem, BreadcrumbLink, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { MenuLayout } from '@/layouts/menu-layout';
import { useApiKeyService } from '@/services/api-key-services';
import { AlertCircle, EllipsisVertical, KeyRound, Plus, RefreshCcw, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { NewApiKeyDialog } from './dialogs/new-key';
import { formatDistanceToNow } from '@/extensions';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { useDeleteApiKey } from '@/hooks/data/api-keys';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useDebounce } from '@/hooks/use-debounce';

const ApiKeyContextMenu = ({ id, onDeleteApiKey }: { id: string; onDeleteApiKey: (id: string) => void }) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <div className="width-10">
          <EllipsisVertical size="14" />
        </div>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-56" align="start">
        <ConfirmDialog
          title={t('Delete key')}
          description={t('Are you sure you want to delete this key?')}
          acceptText={t('Delete')}
          onAccept={async () => {
            onDeleteApiKey(id);
            setOpen(false);
          }}
          onCancel={() => setOpen(false)}>
          <DropdownMenuItem className="text-destructive" onSelect={(e) => e.preventDefault()}>
            <Trash2 className="text-destructive" /> {t('Delete')}
          </DropdownMenuItem>
        </ConfirmDialog>
      </DropdownMenuContent>
    </DropdownMenu>
  );
};

export default function ApiKeys() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, error, isFetching, reload } = useApiKeyService();
  const [showNewApiKeyDialog, setShowNewApiKeyDialog] = useState(false);
  const { error: deleteError, deleteAsync } = useDeleteApiKey();
  const [searchValue, setSearchValue] = useState('');
  const debouncedSearchValue = useDebounce(searchValue, 500);
  const queryString = useMemo(() => {
    return debouncedSearchValue ? `search=${encodeURIComponent(debouncedSearchValue)}` : '';
  }, [debouncedSearchValue]);
  useEffect(() => {
    reload(queryString);
  }, [queryString]);

  const onCreateNewApiKey = () => {
    setShowNewApiKeyDialog(true);
  };

  const onDeleteApiKey = async (keyName: string) => {
    await deleteAsync(keyName);
    navigate('/keys');
    reload(queryString);
  };

  return (
    <>
      <NewApiKeyDialog
        showDialog={showNewApiKeyDialog}
        onClose={() => setShowNewApiKeyDialog(false)}
        reload={() => reload(queryString)}
      />
      <MenuLayout
        searchValue={searchValue}
        onSearchValueChange={(value) => setSearchValue(value)}
        breadcrumb={
          <>
            <BreadcrumbItem className="hidden md:block">
              <BreadcrumbLink onClick={() => navigate('/')} href="#">
                Home
              </BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator className="hidden md:block" />
            <BreadcrumbItem>
              <BreadcrumbPage>{t('ApiKeys')}</BreadcrumbPage>
            </BreadcrumbItem>
          </>
        }
        buttons={
          <>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button className="size-6" size="icon" variant="secondary" onClick={() => reload(queryString)}>
                  <RefreshCcw />
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                <p>{t('Reload keys')}</p>
              </TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button className="size-6" size="icon" variant="default" onClick={onCreateNewApiKey}>
                  <Plus />
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                <p>{t('New item')}</p>
              </TooltipContent>
            </Tooltip>
          </>
        }>
        {deleteError && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error deleting key')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        )}
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error loading keys')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {isFetching && (
          <div className="text-center">
            <Button variant="secondary" disabled size="sm">
              <Spinner />
              {t('Loading keys...')}
            </Button>
          </div>
        )}
        {data?.items?.length === 0 && (
          <div className="p-2 text-muted-foreground">
            {t('No keys found.')}{' '}
            <a
              href="#"
              onClick={(e) => {
                e.preventDefault();
                onCreateNewApiKey();
              }}>
              {t('Please create a new one.')}
            </a>
          </div>
        )}
        {data?.items?.map((key) => (
          <Link
            to={`/keys/${key.id}`}
            key={key.id}
            className="hover:bg-sidebar-accent hover:text-sidebar-accent-foreground flex flex-col items-start gap-2 border-b p-4 text-sm leading-tight last:border-b-0">
            <div className="flex w-full justify-between items-center">
              <span className="font-medium flex flex-row items-center gap-2">
                <KeyRound size={16} /> <span className="w-min-0 break-all max-w-55 truncate">{key.identity.name}</span>
              </span>
              <ApiKeyContextMenu onDeleteApiKey={onDeleteApiKey} id={key.id} />
            </div>
            <div className="text-xs flex flex-col gap-2">
              {key.description && <span>{key.description}</span>}
              <span>
                {t('Expires: ')} {formatDistanceToNow(key.expiresAt)}
              </span>
              <div className="text-xs flex flex-row gap-2 break-all">
                <span>{formatDistanceToNow(key.createdAt)}</span> {t('by')} <span>{key.createdBy?.name}</span>
              </div>
            </div>
            {/* <div className="flex w-full items-center gap-2">
              <span>{mail.name}</span>{" "}
              <span className="ml-auto text-xs">{mail.date}</span>
            </div>
            <span className="font-medium">{mail.subject}</span>
            <span className="line-clamp-2 w-[260px] text-xs whitespace-break-spaces">
              {mail.teaser}
            </span> */}
          </Link>
        ))}
      </MenuLayout>
    </>
  );
}
