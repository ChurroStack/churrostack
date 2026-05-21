import { DateTimeRangePicker } from '@/components/date-time-range-picker';
import { DataTablePagination } from '@/components/table/data-table-pagination';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDateTime, formatDistanceToNow } from '@/extensions';
import { useGetApplicationTraces, type ApplicationTraceItem } from '@/hooks/data/applications';
import { cn } from '@/lib/utils';
import IdentityPicker from '@/pickers/identity-picker';
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
import { AlertCircle, RefreshCcw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const columns: ColumnDef<ApplicationTraceItem>[] = [
  {
    accessorKey: 'timestamp',
    header: 'Timestamp',
    cell: ({ row }) => <div>{formatDateTime(row.getValue('timestamp'))}</div>
  },
  {
    accessorKey: 'clientIp',
    header: 'IP',
    cell: ({ row }) => <div>{row.getValue('clientIp')}</div>
  },
  {
    accessorKey: 'identityName',
    header: 'Identity',
    cell: ({ row }) => <div>{row.getValue('identityName')}</div>
  },
  {
    accessorKey: 'service',
    header: 'Service',
    cell: ({ row }) => <div>{row.getValue('service')}</div>
  },
  {
    accessorKey: 'method',
    header: 'Method',
    cell: ({ row }) => <div>{row.getValue('method')}</div>
  },
  {
    accessorKey: 'path',
    header: 'Path',
    cell: ({ row }) => <div>{row.getValue('path')}</div>
  },
  {
    accessorKey: 'statusCode',
    header: 'Status',
    cell: ({ row }) => <div>{row.getValue('statusCode')}</div>
  }
];

const TracesPanel = ({ appName }: { appName: string }) => {
  const {
    fetchAsync: fetchTracesAsync,
    data: tracesData,
    isFetching: isFetchingTraces,
    error: tracesError
  } = useGetApplicationTraces(appName);
  const { t } = useTranslation();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({});
  const [rowSelection, setRowSelection] = useState({});
  const [page, setPage] = useState(0);
  const [identityName, setIdentiyName] = useState<string | undefined>();
  const [pageSize, setPageSize] = useState(25);
  const [fromDate, setFromDate] = useState<Date | undefined>(() => {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    return date;
  });
  const [toDate, setToDate] = useState<Date | undefined>(() => {
    const date = new Date();
    date.setHours(23, 59, 59, 999);
    return date;
  });

  const table = useReactTable({
    data: tracesData?.items ?? [],
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
      rowSelection,
      pagination: {
        pageIndex: page,
        pageSize: pageSize
      }
    },
    rowCount: tracesData?.count ?? 0,
    manualPagination: true,
    onPaginationChange: (updater) => {
      const newState = typeof updater === 'function' ? updater(table.getState().pagination) : updater;
      setPage(newState.pageIndex);
      setPageSize(newState.pageSize);
    }
  });

  const lastUpdate = useMemo(() => {
    if (!tracesData || tracesData.items.length === 0) {
      return null;
    }
    const maxTimestamp = tracesData.items.reduce((max, item) => {
      const time = Date.parse(item.timestamp);
      return time > max ? time : max;
    }, -Infinity);

    return formatDistanceToNow(new Date(maxTimestamp));
  }, [tracesData]);

  const loadData = () => {
    fetchTracesAsync(
      `page=${page + 1}&pageSize=${pageSize}&identityName=${identityName ? encodeURIComponent(identityName) : ''}` +
        '&from=' +
        (fromDate ? fromDate.toISOString() : '') +
        '&to=' +
        (toDate ? toDate.toISOString() : '')
    );
  };

  useEffect(() => {
    loadData();
  }, [page, pageSize, identityName, fromDate, toDate]);

  return (
    <div className="rounded-md border flex flex-col min-h-0 w-full h-full overflow-auto">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          {lastUpdate && (
            <span className="text-muted-foreground text-sm">
              {t('Updated')} {lastUpdate}
            </span>
          )}
        </div>
        <div className="flex flex-row gap-2 items-center">
          <IdentityPicker className="min-w-30 min-h-8" value={identityName} onChange={(e) => setIdentiyName(e)} />
          <DateTimeRangePicker
            initialDateFrom={fromDate}
            initialDateTo={toDate}
            onUpdate={(o) => {
              setFromDate(o.range.from);
              setToDate(o.range.to);
            }}
            className="sm:w-100"
          />
          <Button variant="secondary" size="sm" onClick={() => loadData()} disabled={isFetchingTraces}>
            {isFetchingTraces ? <Spinner /> : <RefreshCcw />} {t('Refresh')}
          </Button>
        </div>
      </div>
      {tracesError && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error loading application traces')}</AlertTitle>
          <AlertDescription>{tracesError}</AlertDescription>
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
                className={cn(row.original.isError && 'bg-red-100')}>
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
      <DataTablePagination table={table} />
    </div>
  );
};

export default TracesPanel;
