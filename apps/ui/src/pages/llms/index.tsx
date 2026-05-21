import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { BreadcrumbItem, BreadcrumbLink, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { MenuLayout } from '@/layouts/menu-layout';
import { useLlmService } from '@/services/llm-services';
import { AlertCircle, Brain, EllipsisVertical, Plus, RefreshCcw, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { NewLlmDialog } from './dialogs/new-llm';
import { formatDistanceToNow } from '@/extensions';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { useDeleteLlm } from '@/hooks/data/llms';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useProfile } from '@/hooks/data/profile';
import { useDebounce } from '@/hooks/use-debounce';

const LlmContextMenu = ({ id, onDeleteLlm }: { id: string; onDeleteLlm: (id: string) => void }) => {
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
          title={t('Delete llm')}
          description={t('Are you sure you want to delete this llm?')}
          acceptText={t('Delete')}
          onAccept={async () => {
            onDeleteLlm(id);
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

export default function Llms() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, error, isFetching, reload } = useLlmService();
  const [showNewLlmDialog, setShowNewLlmDialog] = useState(false);
  const { error: deleteError, deleteAsync } = useDeleteLlm();
  const { profile } = useProfile();

  const [searchValue, setSearchValue] = useState('');
  const debouncedSearchValue = useDebounce(searchValue, 500);
  const queryString = useMemo(() => {
    return debouncedSearchValue ? `search=${encodeURIComponent(debouncedSearchValue)}` : '';
  }, [debouncedSearchValue]);
  useEffect(() => {
    reload(queryString);
  }, [queryString]);

  const onCreateNewLlm = () => {
    setShowNewLlmDialog(true);
  };

  const onDeleteLlm = async (id: string) => {
    await deleteAsync(id);
    navigate('/llms');
    reload(queryString);
  };

  return (
    <>
      <NewLlmDialog
        showDialog={showNewLlmDialog}
        onClose={() => setShowNewLlmDialog(false)}
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
              <BreadcrumbPage>{t('Llms')}</BreadcrumbPage>
            </BreadcrumbItem>
          </>
        }
        buttons={
          <>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button size="icon" className="size-6" variant="secondary" onClick={() => reload(queryString)}>
                  <RefreshCcw />
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                <p>{t('Reload llms')}</p>
              </TooltipContent>
            </Tooltip>
            {profile?.role === 'administrator' && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button size="icon" className="size-6" variant="default" onClick={onCreateNewLlm}>
                    <Plus />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>
                  <p>{t('New item')}</p>
                </TooltipContent>
              </Tooltip>
            )}
          </>
        }>
        {deleteError && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error deleting llm')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        )}
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error loading llms')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {isFetching && (
          <div className="text-center">
            <Button variant="secondary" disabled size="sm">
              <Spinner />
              {t('Loading llms...')}
            </Button>
          </div>
        )}
        {data?.items?.length === 0 && (
          <div className="p-2 text-muted-foreground">
            {t('No llms found.')}{' '}
            <a
              href="#"
              onClick={(e) => {
                e.preventDefault();
                onCreateNewLlm();
              }}>
              {t('Please create a new one.')}
            </a>
          </div>
        )}
        {data?.items?.map((llm) => (
          <Link
            to={`/llms/${llm.id}`}
            key={llm.id}
            className="hover:bg-sidebar-accent hover:text-sidebar-accent-foreground flex flex-col items-start gap-2 border-b p-4 text-sm leading-tight last:border-b-0">
            <div className="flex w-full justify-between items-center">
              <span className="font-medium flex flex-row items-center gap-2">
                <Brain size={16} />{' '}
                <span className="w-min-0 break-all max-w-55 truncate">
                  {llm.names?.length && llm.names.length > 0 ? llm.names[0] : llm.id}
                </span>
              </span>
              <LlmContextMenu onDeleteLlm={onDeleteLlm} id={llm.id} />
            </div>
            <div className="text-xs break-all">
              <span>{formatDistanceToNow(llm.createdAt)}</span> {t('by')} <span>{llm.createdBy?.name}</span>
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
