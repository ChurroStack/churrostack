import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { formatBytes, formatDistanceToNow, formatPercent } from '@/extensions';
import { getApplicationStatus, useGetApplications } from '@/hooks/data/applications';
import { AppStatus } from '@/pages/applications/common/app-status';
import { AppWindow, Cpu, MemoryStick, Tag, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { SearchAndFilter } from '@/components/search-and-filter';
import { ApplicationsFilterContent } from '@/pages/applications/filters/applications-filter';
import { TagBadges } from '@/components/tag-badges';
import { Badge } from '@/components/ui/badge';
import { useDebounce } from '@/hooks/use-debounce';
import { User } from 'lucide-react';

function EnvironmentsApplicationsPanel({ environmentName }: { environmentName: string }) {
  const { t } = useTranslation();
  const { fetchAsync: fetchApplications, data: apps } = useGetApplications();
  const [searchValue, setSearchValue] = useState('');
  const debouncedSearch = useDebounce(searchValue, 500);
  const [tagsFilter, setTagsFilter] = useState<string[]>([]);
  const [createdByFilter, setCreatedByFilter] = useState<string | undefined>(undefined);

  const queryString = useMemo(() => {
    const parts: string[] = [`environment=${encodeURIComponent(environmentName)}`];
    if (debouncedSearch) parts.push(`search=${encodeURIComponent(debouncedSearch)}`);
    if (createdByFilter) parts.push(`createdBy=${encodeURIComponent(createdByFilter)}`);
    for (const tag of tagsFilter) parts.push(`tags=${encodeURIComponent(tag)}`);
    return parts.join('&');
  }, [environmentName, debouncedSearch, tagsFilter, createdByFilter]);

  useEffect(() => {
    fetchApplications(queryString);
  }, [queryString, fetchApplications]);

  return (
    <div className="flex flex-col h-full">
      <div className="p-4 pb-2 flex flex-col gap-2">
        <SearchAndFilter
          searchValue={searchValue}
          onSearchValueChange={setSearchValue}
          placeholder={t('Search applications...')}
          hasActiveFilter={tagsFilter.length > 0 || !!createdByFilter}
          filterContent={
            <ApplicationsFilterContent
              createdBy={createdByFilter}
              tags={tagsFilter}
              permission="read"
              hideEnvironment
              onCreatedByChange={setCreatedByFilter}
              onTagsChange={setTagsFilter}
            />
          }
          activeBadges={
            tagsFilter.length > 0 || createdByFilter ? (
              <div className="flex flex-row flex-wrap gap-1">
                {createdByFilter && (
                  <Badge variant="secondary" className="gap-1">
                    <User className="size-3" />
                    <span className="max-w-40 truncate">{createdByFilter}</span>
                    <button
                      type="button"
                      aria-label={t('Clear created by filter')}
                      onClick={() => setCreatedByFilter(undefined)}
                      className="ml-1 hover:text-foreground">
                      <X className="size-3" />
                    </button>
                  </Badge>
                )}
                {tagsFilter.map((tag) => (
                  <Badge key={tag} variant="secondary" className="gap-1">
                    <Tag className="size-3" />
                    <span className="max-w-40 truncate">{tag}</span>
                    <button
                      type="button"
                      aria-label={t('Clear tag filter')}
                      onClick={() => setTagsFilter((prev) => prev.filter((x) => x !== tag))}
                      className="ml-1 hover:text-foreground">
                      <X className="size-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            ) : null
          }
        />
      </div>
      <div className="p-4 pt-2 flex-1 overflow-auto bg-[radial-gradient(circle,_rgba(0,0,0,0.15)_1px,_transparent_1px)] dark:bg-[radial-gradient(circle,_rgba(255,255,255,0.15)_1px,_transparent_1px)] bg-[length:20px_20px]">
        {apps && apps.items && (
          <div
            className="
            w-full
            grid
            grid-cols-1
            sm:grid-cols-2
            lg:grid-cols-3
            xl:grid-cols-4
            gap-4">
            {apps.items.map((application) => {
              const cpuPct = application.metrics?.cpu_usage_pct;
              const memPct = application.metrics?.memory_usage_pct;
              const cpuLimit = application.metrics?.cpu_limit;
              const memLimit = application.metrics?.memory_limit;
              const peakPct = Math.max(cpuPct ?? 0, memPct ?? 0);
              const cpuOfLimit =
                cpuLimit !== undefined
                  ? cpuLimit < 1
                    ? `${Math.round(cpuLimit * 1000)}m`
                    : Number.isInteger(cpuLimit)
                      ? `${cpuLimit} ${cpuLimit === 1 ? t('core') : t('cores')}`
                      : `${cpuLimit.toFixed(2)} ${t('cores')}`
                  : t('limit');
              const memOfLimit = memLimit !== undefined ? formatBytes(memLimit) : t('limit');
              const cardClass =
                peakPct >= 0.9
                  ? 'bg-red-50 border-red-500 dark:bg-red-950/40 dark:border-red-500'
                  : peakPct >= 0.7
                    ? 'bg-orange-50 border-orange-500 dark:bg-orange-950/40 dark:border-orange-500'
                    : 'bg-white border dark:bg-gray-800 dark:border-gray-700';
              return (
                <div
                  className={`flex flex-col shadow-sm rounded-md w-full cursor-pointer min-w-60 border ${cardClass}`}
                  key={application.name}>
                  <Link
                    to={`/applications/${application.name}`}
                    key={application.name}
                    className="hover:bg-sidebar-accent hover:text-sidebar-accent-foreground flex flex-col items-start gap-2 border-b p-4 text-sm leading-tight last:border-b-0">
                    <div className="flex w-full justify-between items-center">
                      <span className="font-medium flex flex-row items-center gap-2 cursor-pointer">
                        <AppWindow size={16} />{' '}
                        <span className="w-min-0 break-all max-w-55 truncate cursor-pointer">{application.name}</span>
                      </span>
                    </div>
                    <div className="flex flex-row gap-4 justify-start w-full items-center text-muted-foreground text-xs">
                      <Tooltip>
                        <TooltipTrigger>
                          <div className="flex flex-row gap-1">
                            <Cpu className="size-4" /> {cpuPct !== undefined ? `${formatPercent(cpuPct)}%` : '-'}
                          </div>
                        </TooltipTrigger>
                        <TooltipContent>
                          {cpuPct !== undefined
                            ? `${t('CPU usage')}: ${formatPercent(cpuPct)}% ${t('of')} ${cpuOfLimit}`
                            : t('CPU usage')}
                        </TooltipContent>
                      </Tooltip>
                      <Tooltip>
                        <TooltipTrigger>
                          <div className="flex flex-row gap-1">
                            <MemoryStick className="size-4 rotate-135" />{' '}
                            {formatBytes(parseFloat(`${application.metrics?.memory_usage ?? '0'}`)) ?? '-'}
                          </div>
                        </TooltipTrigger>
                        <TooltipContent>
                          {memPct !== undefined
                            ? `${t('Memory usage')}: ${formatPercent(memPct)}% ${t('of')} ${memOfLimit}`
                            : t('Memory usage (bytes)')}
                        </TooltipContent>
                      </Tooltip>
                      <AppStatus
                        status={getApplicationStatus(application.provisionStatus, application.executionStatus)}
                      />
                    </div>
                    <div className="text-xs break-all">
                      <span>{formatDistanceToNow(application.createdAt)}</span> {t('by')}{' '}
                      <span>{application.createdBy?.name}</span>
                    </div>
                    <TagBadges tags={application.tags} />
                  </Link>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

export default EnvironmentsApplicationsPanel;
