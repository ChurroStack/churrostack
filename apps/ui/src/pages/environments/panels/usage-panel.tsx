import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow
} from '@/components/ui/table';
import { formatBytes, formatDistanceToNow } from '@/extensions';
import { getApplicationStatus, type ApplicationStatus } from '@/hooks/data/applications';
import {
  useAnalyzeEnvironmentUsage,
  useGetEnvironmentUsage,
  type EnvironmentUsageItem
} from '@/hooks/data/environments';
import { AppStatus } from '@/pages/applications/common/app-status';
import { AlertCircle, ArrowDown, ArrowUp, ChevronDown, ChevronUp, Gauge } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { toast } from 'sonner';
import { SearchAndFilter } from '@/components/search-and-filter';
import { ApplicationsFilterContent } from '@/pages/applications/filters/applications-filter';
import { useDebounce } from '@/hooks/use-debounce';

function RecommendationCell({ usage }: { usage: EnvironmentUsageItem }) {
  const { t } = useTranslation();

  if (usage.direction === 'not_analyzed') {
    return <span className="text-muted-foreground text-xs">{t('Not analyzed')}</span>;
  }
  if (usage.direction === 'insufficient_data') {
    return <span className="text-muted-foreground text-xs">{t('Not enough data')}</span>;
  }
  if (!usage.hasRecommendation || !usage.recommendedSize) {
    return (
      <Badge variant="outline" className="text-green-700 border-green-600">
        {t('Optimal')}
      </Badge>
    );
  }

  const Icon = usage.direction === 'upsize' ? ArrowUp : ArrowDown;
  return (
    <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-600">
      <Icon className="size-3" /> {usage.recommendedSize.hint}
    </Badge>
  );
}

type SortKey =
  | 'applicationName'
  | 'status'
  | 'currentSize'
  | 'cpuMax'
  | 'cpuP95'
  | 'memoryMax'
  | 'memoryP95'
  | 'computedAt';

const NUMERIC_SORT_KEYS: ReadonlyArray<SortKey> = ['cpuMax', 'cpuP95', 'memoryMax', 'memoryP95'];

// Order users actually expect when grouping apps: alive at the top, problems at the bottom.
const STATUS_PRIORITY: Record<ApplicationStatus, number> = {
  running: 0,
  starting: 1,
  provisioning: 2,
  pending: 3,
  stopping: 4,
  stopped: 5,
  failed: 6
};

function compareRows(a: EnvironmentUsageItem, b: EnvironmentUsageItem, key: SortKey): number {
  switch (key) {
    case 'applicationName':
      return a.applicationName.localeCompare(b.applicationName);
    case 'status': {
      const sa = STATUS_PRIORITY[getApplicationStatus(a.provisionStatus, a.executionStatus)];
      const sb = STATUS_PRIORITY[getApplicationStatus(b.provisionStatus, b.executionStatus)];
      return sa - sb;
    }
    case 'currentSize':
      return (a.currentSize?.hint ?? '').localeCompare(b.currentSize?.hint ?? '');
    case 'cpuMax':
      return a.cpuMax - b.cpuMax;
    case 'cpuP95':
      return a.cpuP95 - b.cpuP95;
    case 'memoryMax':
      return a.memoryMax - b.memoryMax;
    case 'memoryP95':
      return a.memoryP95 - b.memoryP95;
    case 'computedAt':
      return (a.computedAt ?? '').localeCompare(b.computedAt ?? '');
  }
}

function SortableHeader({
  label,
  sortKey,
  sort,
  onSortChange
}: {
  label: string;
  sortKey: SortKey;
  sort: { key: SortKey; dir: 'asc' | 'desc' };
  onSortChange: (key: SortKey) => void;
}) {
  const active = sort.key === sortKey;
  const Icon = active ? (sort.dir === 'asc' ? ChevronUp : ChevronDown) : null;
  return (
    <TableHead aria-sort={active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button
        type="button"
        onClick={() => onSortChange(sortKey)}
        className="inline-flex items-center gap-1 hover:text-foreground">
        {label}
        {Icon && <Icon className="size-3" />}
      </button>
    </TableHead>
  );
}

const EnvironmentUsagePanel = ({
  environmentName,
  reloadSignal
}: {
  environmentName: string;
  reloadSignal?: number;
}) => {
  const { t } = useTranslation();
  const { data: usage, fetchAsync, isFetching, error } = useGetEnvironmentUsage(environmentName);
  const { postAsync: analyzeAllAsync, isFetching: isAnalyzing } = useAnalyzeEnvironmentUsage(environmentName);

  const [sort, setSort] = useState<{ key: SortKey; dir: 'asc' | 'desc' }>({
    key: 'applicationName',
    dir: 'asc'
  });

  const [searchValue, setSearchValue] = useState('');
  const debouncedSearch = useDebounce(searchValue, 250).toLowerCase();
  const [createdByFilter, setCreatedByFilter] = useState<string | undefined>(undefined);
  const [tagsFilter, setTagsFilter] = useState<string[]>([]);

  const onSortChange = (key: SortKey) => {
    setSort((prev) => {
      if (prev.key === key) {
        return { key, dir: prev.dir === 'asc' ? 'desc' : 'asc' };
      }
      return { key, dir: NUMERIC_SORT_KEYS.includes(key) ? 'desc' : 'asc' };
    });
  };

  const filteredUsage = useMemo(() => {
    if (!usage) return undefined;
    return usage.filter((row) => {
      if (debouncedSearch && !row.applicationName.toLowerCase().includes(debouncedSearch)) return false;
      if (createdByFilter && row.createdBy !== createdByFilter) return false;
      if (tagsFilter.length > 0 && !tagsFilter.every((t) => row.tags?.includes(t))) return false;
      return true;
    });
  }, [usage, debouncedSearch, createdByFilter, tagsFilter]);

  const sortedUsage = useMemo(() => {
    if (!filteredUsage) return undefined;
    const rows = [...filteredUsage];
    rows.sort((a, b) => {
      const cmp = compareRows(a, b, sort.key);
      return sort.dir === 'asc' ? cmp : -cmp;
    });
    return rows;
  }, [filteredUsage, sort]);

  const totals = useMemo(() => {
    if (!filteredUsage?.length) return undefined;
    return filteredUsage.reduce(
      (acc, r) => ({
        cpuAvg: acc.cpuAvg + r.cpuAvg,
        cpuMax: acc.cpuMax + r.cpuMax,
        cpuP95: acc.cpuP95 + r.cpuP95,
        memoryAvg: acc.memoryAvg + r.memoryAvg,
        memoryMax: acc.memoryMax + r.memoryMax,
        memoryP95: acc.memoryP95 + r.memoryP95
      }),
      { cpuAvg: 0, cpuMax: 0, cpuP95: 0, memoryAvg: 0, memoryMax: 0, memoryP95: 0 }
    );
  }, [filteredUsage]);

  useEffect(() => {
    fetchAsync('');
  }, [environmentName, fetchAsync, reloadSignal]);

  const onAnalyzeAll = async () => {
    const result = await analyzeAllAsync();
    if (result.error) {
      toast.error(result.error);
      return;
    }
    toast.success(t('Usage analysis completed.'));
    await fetchAsync('');
  };

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 items-center gap-2">
        <h3 className="text-sm text-muted-foreground flex-1 min-w-0 truncate">
          {t('Historic CPU and memory usage and recommended size per application')}
        </h3>
        <div className="w-50 shrink-0">
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
          />
        </div>
        <Button variant="default" size="sm" disabled={isAnalyzing} onClick={onAnalyzeAll}>
          {isAnalyzing ? <Spinner /> : <Gauge />} {t('Analyze all')}
        </Button>
      </div>
      {error && (
        <div className="p-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error loading usage')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col h-full overflow-auto">
        <Table>
          <TableHeader>
            <TableRow>
              <SortableHeader
                label={t('Application')}
                sortKey="applicationName"
                sort={sort}
                onSortChange={onSortChange}
              />
              <SortableHeader label={t('Status')} sortKey="status" sort={sort} onSortChange={onSortChange} />
              <SortableHeader
                label={t('Current size')}
                sortKey="currentSize"
                sort={sort}
                onSortChange={onSortChange}
              />
              <SortableHeader
                label={t('CPU avg / max (cores)')}
                sortKey="cpuMax"
                sort={sort}
                onSortChange={onSortChange}
              />
              <SortableHeader
                label={t('CPU P95 (cores)')}
                sortKey="cpuP95"
                sort={sort}
                onSortChange={onSortChange}
              />
              <SortableHeader
                label={t('Memory avg / max')}
                sortKey="memoryMax"
                sort={sort}
                onSortChange={onSortChange}
              />
              <SortableHeader
                label={t('Memory P95')}
                sortKey="memoryP95"
                sort={sort}
                onSortChange={onSortChange}
              />
              <TableHead>{t('Recommended size')}</TableHead>
              <SortableHeader
                label={t('Analyzed')}
                sortKey="computedAt"
                sort={sort}
                onSortChange={onSortChange}
              />
            </TableRow>
          </TableHeader>
          <TableBody>
            {sortedUsage?.map((row) => (
              <TableRow key={row.applicationName}>
                <TableCell className="font-medium">
                  <Link to={`/applications/${row.applicationName}`} className="hover:underline">
                    {row.applicationName}
                  </Link>
                </TableCell>
                <TableCell>
                  <AppStatus status={getApplicationStatus(row.provisionStatus, row.executionStatus)} />
                </TableCell>
                <TableCell className="font-mono text-xs">{row.currentSize?.hint ?? '-'}</TableCell>
                <TableCell className="font-mono text-xs">
                  {row.computedAt ? `${row.cpuAvg.toFixed(2)} / ${row.cpuMax.toFixed(2)}` : '-'}
                </TableCell>
                <TableCell className="font-mono text-xs">
                  {row.computedAt ? row.cpuP95.toFixed(2) : '-'}
                </TableCell>
                <TableCell className="font-mono text-xs">
                  {row.computedAt ? `${formatBytes(row.memoryAvg)} / ${formatBytes(row.memoryMax)}` : '-'}
                </TableCell>
                <TableCell className="font-mono text-xs">
                  {row.computedAt ? formatBytes(row.memoryP95) : '-'}
                </TableCell>
                <TableCell>
                  <RecommendationCell usage={row} />
                </TableCell>
                <TableCell className="text-xs text-muted-foreground">
                  {row.computedAt ? formatDistanceToNow(row.computedAt) : '-'}
                </TableCell>
              </TableRow>
            ))}
            {sortedUsage && sortedUsage.length === 0 && (
              <TableRow>
                <TableCell colSpan={9} className="text-center text-muted-foreground py-6">
                  {usage && usage.length > 0
                    ? t('No applications match the current filters.')
                    : t('No applications in this environment.')}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
          {totals && (
            <TableFooter>
              <TableRow>
                <TableCell colSpan={3} className="font-semibold">
                  {t('Total')}
                </TableCell>
                <TableCell className="font-mono text-xs font-semibold">
                  {`${totals.cpuAvg.toFixed(2)} / ${totals.cpuMax.toFixed(2)}`}
                </TableCell>
                <TableCell className="font-mono text-xs font-semibold">{totals.cpuP95.toFixed(2)}</TableCell>
                <TableCell className="font-mono text-xs font-semibold">
                  {`${formatBytes(totals.memoryAvg)} / ${formatBytes(totals.memoryMax)}`}
                </TableCell>
                <TableCell className="font-mono text-xs font-semibold">{formatBytes(totals.memoryP95)}</TableCell>
                <TableCell />
                <TableCell />
              </TableRow>
            </TableFooter>
          )}
        </Table>
        {isFetching && !usage && (
          <div className="flex justify-center p-6">
            <Spinner />
          </div>
        )}
      </div>
    </div>
  );
};

export default EnvironmentUsagePanel;
