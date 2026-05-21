import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { BreadcrumbItem, BreadcrumbLink, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { MenuLayout } from '@/layouts/menu-layout';
import { useTemplateService } from '@/services/template-services';
import { AlertCircle, EllipsisVertical, Plus, RefreshCcw, FileBraces, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { NewTemplateDialog } from './dialogs/new-template';
import { formatDistanceToNow } from '@/extensions';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { useDeleteTemplate } from '@/hooks/data/templates';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useDebounce } from '@/hooks/use-debounce';

const TemplateContextMenu = ({
  name,
  target,
  onDeleteTemplate
}: {
  name: string;
  target: string;
  onDeleteTemplate: (name: string, target: string) => void;
}) => {
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
          title={t('Delete template')}
          description={t('Are you sure you want to delete this template?')}
          acceptText={t('Delete')}
          onAccept={async () => {
            onDeleteTemplate(name, target);
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

export default function Templates() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, error, isFetching, reload } = useTemplateService();
  const [showNewTemplateDialog, setShowNewTemplateDialog] = useState(false);
  const { error: deleteError, deleteAsync } = useDeleteTemplate();

  const [searchValue, setSearchValue] = useState('');
  const debouncedSearchValue = useDebounce(searchValue, 500);
  const queryString = useMemo(() => {
    return debouncedSearchValue ? `search=${encodeURIComponent(debouncedSearchValue)}` : '';
  }, [debouncedSearchValue]);
  useEffect(() => {
    reload(queryString);
  }, [queryString]);

  const onCreateNewTemplate = () => {
    setShowNewTemplateDialog(true);
  };

  const onDeleteTemplate = async (templateName: string, target: string) => {
    await deleteAsync(`${templateName}:${target}`);
    navigate('/templates');
    reload(queryString);
  };

  return (
    <>
      <NewTemplateDialog
        showDialog={showNewTemplateDialog}
        onClose={() => setShowNewTemplateDialog(false)}
        reload={() => reload(queryString)}
      />
      <MenuLayout
        searchValue={searchValue}
        onSearchValueChange={setSearchValue}
        breadcrumb={
          <>
            <BreadcrumbItem className="hidden md:block">
              <BreadcrumbLink onClick={() => navigate('/')} href="#">
                Home
              </BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator className="hidden md:block" />
            <BreadcrumbItem>
              <BreadcrumbPage>Templates</BreadcrumbPage>
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
                <p>{t('Reload templates')}</p>
              </TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button size="icon" className="size-6" variant="default" onClick={onCreateNewTemplate}>
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
            <AlertTitle>{t('Error deleting template')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        )}
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error loading templates')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {(isFetching || !data) && (
          <div className="text-center">
            <Button variant="secondary" disabled size="sm">
              <Spinner />
              {t('Loading templates...')}
            </Button>
          </div>
        )}
        {data?.items?.length === 0 && (
          <div className="p-2 text-muted-foreground">
            {t('No templates found.')}{' '}
            <a
              href="#"
              onClick={(e) => {
                e.preventDefault();
                onCreateNewTemplate();
              }}>
              {t('Please create a new one.')}
            </a>
          </div>
        )}
        {data?.items?.map((template) => (
          <Link
            to={`/templates/${template.name}:${template.target}`}
            key={template.name}
            className="hover:bg-sidebar-accent hover:text-sidebar-accent-foreground flex flex-col items-start gap-2 border-b p-4 text-sm leading-tight last:border-b-0">
            <div className="flex w-full justify-between items-center">
              <span className="font-medium flex flex-row items-center gap-2 w-full">
                <FileBraces size={16} />{' '}
                <div className="line-clamp-3 break-all truncate w-[200px]">{template.title ?? template.name}</div>
              </span>
              <TemplateContextMenu onDeleteTemplate={onDeleteTemplate} name={template.name} target={template.target} />
            </div>
            <p className="w-60 truncate line-clamp-3 break-all">{template.description}</p>
            <div className="text-xs break-all">
              <span>{formatDistanceToNow(template?.createdAt)}</span> {t('by')} <span>{template?.createdBy?.name}</span>
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
