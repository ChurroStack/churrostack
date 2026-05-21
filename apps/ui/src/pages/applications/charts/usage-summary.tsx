import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatBytes, formatDateTime } from '@/extensions';
import { useGetApplicationUsage, type ApplicationUsageItem } from '@/hooks/data/applications';
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
import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

const columns: ColumnDef<ApplicationUsageItem>[] = [
  {
    accessorKey: 'identityName',
    header: 'Name',
    cell: ({ row }) => <div>{row.getValue('identityName')}</div>
  },
  {
    accessorKey: 'to',
    header: 'Last access',
    cell: ({ row }) => <div>{formatDateTime(row.getValue('to'))}</div>
  },
  {
    accessorKey: 'requests',
    header: 'Requests',
    cell: ({ row }) => <div>{row.getValue('requests')}</div>
  },
  {
    accessorKey: 'incomingTraffic',
    header: 'Incoming Traffic',
    cell: ({ row }) => <div>{formatBytes(row.getValue('incomingTraffic'))}</div>
  },
  {
    accessorKey: 'outgoingTraffic',
    header: 'Outgoing Traffic',
    cell: ({ row }) => <div>{formatBytes(row.getValue('outgoingTraffic'))}</div>
  }
];

const ApplicationUsage = ({
  appName,
  fromDate,
  toDate
}: {
  appName: string;
  fromDate: Date | undefined;
  toDate: Date | undefined;
}) => {
  const {
    fetchAsync,
    data: usageData,
    isFetching: isFetchingUsage,
    error: usageError
  } = useGetApplicationUsage(appName, 'identity');
  const { t } = useTranslation();
  const [sorting, setSorting] = useState<SortingState>([]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({});
  const [rowSelection, setRowSelection] = useState({});
  const table = useReactTable({
    data: usageData?.items ?? [],
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

  const fetchUsageAsync = useCallback(async () => {
    fetchAsync(`from=${fromDate?.toISOString() ?? ''}&to=${toDate?.toISOString() ?? ''}`);
  }, [fetchAsync, fromDate, toDate]);

  useEffect(() => {
    fetchUsageAsync();
  }, [fetchUsageAsync]);

  return (
    <div className="rounded-md border flex flex-col min-h-100 w-full h-full overflow-auto ">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h2 className="text-lg font-medium">{t('Usage by Identity')}</h2>
        </div>
        <div className="flex flex-row">
          <Button variant="secondary" size="sm" onClick={() => fetchUsageAsync()} disabled={isFetchingUsage}>
            {isFetchingUsage ? <Spinner /> : <RefreshCcw />} {t('Refresh')}
          </Button>
        </div>
      </div>
      {usageError && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error loading usage statistics')}</AlertTitle>
          <AlertDescription>{usageError}</AlertDescription>
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
              <TableRow key={row.id} data-state={row.getIsSelected() && 'selected'}>
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

export default ApplicationUsage;
