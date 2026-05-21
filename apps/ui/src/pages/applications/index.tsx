import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { BreadcrumbItem, BreadcrumbLink, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { MenuLayout } from '@/layouts/menu-layout';
import { useApplicationService } from '@/services/application-services';
import { AlertCircle, AppWindow, Cpu, Layers, MemoryStick, Plus, RefreshCcw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { NewApplicationDialog } from './dialogs/new-application';
import { formatBytes, formatDistanceToNow, formatPercent } from '@/extensions';
import { getApplicationStatus, useDeleteApplication } from '@/hooks/data/applications';
import ApplicationContextMenu from './menus/application-menu';
import { AppStatus } from './common/app-status';
import { useDebounce } from '@/hooks/use-debounce';

export default function Applications() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data, error, isFetching, reload } = useApplicationService();
  const [showNewApplicationDialog, setShowNewApplicationDialog] = useState(false);
  const { error: deleteError, deleteAsync } = useDeleteApplication();
  const [searchValue, setSearchValue] = useState('');
  const debouncedSearchValue = useDebounce(searchValue, 500);
  const queryString = useMemo(() => {
    return debouncedSearchValue ? `search=${encodeURIComponent(debouncedSearchValue)}` : '';
  }, [debouncedSearchValue]);

  useEffect(() => {
    reload(queryString);
  }, [queryString]);

  const onCreateNewApplication = () => {
    setShowNewApplicationDialog(true);
  };

  const onDeleteApplication = async (applicationName: string) => {
    await deleteAsync(applicationName);
    navigate('/applications');
    reload(queryString);
  };

  return (
    <>
      <NewApplicationDialog
        showDialog={showNewApplicationDialog}
        onClose={() => setShowNewApplicationDialog(false)}
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
              <BreadcrumbPage>Applications</BreadcrumbPage>
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
                <p>{t('Reload applications')}</p>
              </TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button size="icon" className="size-6" variant="default" onClick={onCreateNewApplication}>
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
            <AlertTitle>{t('Error deleting application')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        )}
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error loading applications')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {isFetching && (
          <div className="text-center">
            <Button variant="secondary" disabled size="sm">
              <Spinner />
              {t('Loading applications...')}
            </Button>
          </div>
        )}
        {data?.items?.length === 0 && (
          <div className="p-2 text-muted-foreground">
            {t('No applications found.')}{' '}
            <a
              href="#"
              onClick={(e) => {
                e.preventDefault();
                onCreateNewApplication();
              }}>
              {t('Please create a new one.')}
            </a>
          </div>
        )}
        {data?.items?.map((application) => (
          <Link
            to={`/applications/${application.name}`}
            key={application.name}
            className="hover:bg-sidebar-accent hover:text-sidebar-accent-foreground flex flex-col items-start gap-2 border-b p-4 text-sm leading-tight last:border-b-0">
            <div className="flex w-full justify-between items-center">
              <span className="font-medium flex flex-row items-center gap-2">
                {application.mode == 'workspace' ? <Layers size={16} /> : <AppWindow size={16} />}{' '}
                <span className="w-min-0 break-all max-w-55 truncate">{application.name}</span>
              </span>
              <ApplicationContextMenu onDeleteApplication={onDeleteApplication} name={application.name} />
            </div>
            <div className="flex flex-row gap-4 justify-start w-full items-center text-muted-foreground text-xs">
              <Tooltip>
                <TooltipTrigger>
                  <div className="flex flex-row gap-1">
                    <Cpu className="size-4" />{' '}
                    {application.metrics?.cpu_usage &&
                    application.metrics?.cpu_usage > 0 &&
                    application.executionStatus !== 'stopped'
                      ? formatPercent(application.metrics?.cpu_usage ?? 0)
                      : 0}
                    %
                  </div>
                </TooltipTrigger>
                <TooltipContent>{t('CPU Usage (%)')}</TooltipContent>
              </Tooltip>
              <Tooltip>
                <TooltipTrigger>
                  <div className="flex flex-row gap-1">
                    <MemoryStick className="size-4 rotate-135" />{' '}
                    {formatBytes(
                      application.metrics?.memory_usage &&
                        application.metrics?.memory_usage > 0 &&
                        application.executionStatus !== 'stopped'
                        ? parseFloat(`${application.metrics?.memory_usage ?? '0'}`)
                        : 0
                    ) ?? '-'}
                  </div>
                </TooltipTrigger>
                <TooltipContent>{t('Memory Usage (Bytes)')}</TooltipContent>
              </Tooltip>
              <AppStatus status={getApplicationStatus(application.provisionStatus, application.executionStatus)} />
            </div>
            <div className="text-xs break-all">
              <span>{formatDistanceToNow(application.createdAt)}</span> {t('by')}{' '}
              <span>{application.createdBy?.name}</span>
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
