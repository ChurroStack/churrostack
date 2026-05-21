import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group';
import { Spinner } from '@/components/ui/spinner';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDateTime, formatDistanceToNow } from '@/extensions';
import { useGetApplicationEvents, type ApplicationEventItem } from '@/hooks/data/applications';
import { useDebounce } from '@/hooks/use-debounce';
import { cn } from '@/lib/utils';
import {
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type ColumnFiltersState,
  type SortingState,
  type VisibilityState
} from '@tanstack/react-table';
import { AlertCircle, RefreshCcw, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const columns: ColumnDef<ApplicationEventItem>[] = [
  // {
  //   accessorKey: 'type',
  //   header: 'Type',
  //   cell: ({ row }) => <div className="capitalize">{row.getValue('type')}</div>
  // },
  {
    accessorKey: 'timestamp',
    header: 'Timestamp',
    cell: ({ row }) => <div>{formatDateTime(row.getValue('timestamp'))}</div>
  },
  {
    accessorKey: 'target',
    header: 'Target',
    cell: ({ row }) => <div>{row.getValue('target')}</div>
  },
  {
    accessorKey: 'reason',
    header: 'Reason',
    cell: ({ row }) => <div>{row.getValue('reason')}</div>
  },
  {
    accessorKey: 'message',
    header: 'Message',
    cell: ({ row }) => (
      <div className="break-all whitespace-normal [overflow-wrap:anywhere]">{row.getValue('message')}</div>
    )
  }
];

const EventsPanel = ({ appName }: { appName: string }) => {
  const {
    fetchAsync: fetchEventsAsync,
    data: eventsData,
    isFetching: isFetchingEvents,
    error: eventsError
  } = useGetApplicationEvents(appName);
  const { t } = useTranslation();
  const [autoLoad, setAutoLoad] = useState<boolean>(true);
  const [sorting, setSorting] = useState<SortingState>([{ id: 'timestamp', desc: true }]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({});
  const [rowSelection, setRowSelection] = useState({});

  const [searchValue, setSearchValue] = useState('');
  const debouncedSearchValue = useDebounce(searchValue, 500);
  const queryString = useMemo(() => {
    return debouncedSearchValue ? `search=${encodeURIComponent(debouncedSearchValue)}` : '';
  }, [debouncedSearchValue]);

  const table = useReactTable({
    data: eventsData?.items ?? [],
    columns,
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    onColumnVisibilityChange: setColumnVisibility,
    onRowSelectionChange: setRowSelection,
    state: {
      sorting,
      columnFilters,
      columnVisibility,
      rowSelection
    },
    manualPagination: true
  });

  const lastUpdate = useMemo(() => {
    if (!eventsData || eventsData.items.length === 0) {
      return null;
    }
    const maxTimestamp = eventsData.items.reduce((max, item) => {
      const time = Date.parse(item.timestamp);
      return time > max ? time : max;
    }, -Infinity);

    return formatDistanceToNow(new Date(maxTimestamp));
  }, [eventsData]);

  useEffect(() => {
    fetchEventsAsync(queryString);
  }, [fetchEventsAsync, queryString]);

  useEffect(() => {
    let interval: number;
    if (autoLoad) {
      interval = setInterval(() => {
        fetchEventsAsync(queryString);
      }, 2000);
    }
    return () => {
      if (interval) {
        clearInterval(interval);
      }
    };
  }, [autoLoad, fetchEventsAsync, queryString]);

  return (
    <div className="rounded-md border flex flex-col min-h-0 w-full h-full overflow-auto">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <Switch
            id="auto-refresh-events"
            className="mr-2"
            checked={autoLoad}
            onCheckedChange={(checked) => setAutoLoad(checked)}
          />
          <label htmlFor="auto-refresh-events" className="select-none mr-4 text-muted-foreground text-sm">
            {t('Auto Refresh every 1 second')}
          </label>
          {lastUpdate && (
            <span className="text-muted-foreground text-sm">
              {t('Updated')} {lastUpdate}
            </span>
          )}
        </div>
        <div className="flex flex-row gap-2">
          <InputGroup className="max-w-xs">
            <InputGroupInput
              placeholder={t('Search for events...')}
              value={searchValue}
              onChange={(e) => setSearchValue(e.target.value)}
            />
            <InputGroupAddon>
              <Search />
            </InputGroupAddon>
          </InputGroup>
          <Button variant="secondary" onClick={() => fetchEventsAsync('')} disabled={isFetchingEvents}>
            {isFetchingEvents ? <Spinner /> : <RefreshCcw />} {t('Refresh')}
          </Button>
        </div>
      </div>
      {eventsError && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error loading applications')}</AlertTitle>
          <AlertDescription>{eventsError}</AlertDescription>
        </Alert>
      )}
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id}>
              {headerGroup.headers.map((header) => {
                return (
                  <TableHead key={header.id}>
                    {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                );
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows?.length ? (
            table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                data-state={row.getIsSelected() && 'selected'}
                className={cn(
                  row.original.type == 'Warning' && 'bg-yellow-100',
                  row.original.type == 'Error' && 'bg-red-100'
                )}>
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                ))}
              </TableRow>
            ))
          ) : (
            <TableRow>
              <TableCell colSpan={columns.length} className="h-24 text-center">
                {t('No results')}
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </div>
  );
};

export default EventsPanel;
