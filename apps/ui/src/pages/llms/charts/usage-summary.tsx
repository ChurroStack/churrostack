import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useGetLlmUsage, type LlmUsageSummaryItem } from '@/hooks/data/llms';
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

const columns: ColumnDef<LlmUsageSummaryItem>[] = [
  {
    accessorKey: 'name',
    header: 'Name',
    cell: ({ row }) => <div>{row.getValue('name')}</div>
  },
  {
    accessorKey: 'completions',
    header: 'Completions',
    cell: ({ row }) => <div>{row.getValue('completions')}</div>
  },
  {
    accessorKey: 'promptTokens',
    header: 'Prompt Tokens',
    cell: ({ row }) => <div>{row.getValue('promptTokens')}</div>
  },
  {
    accessorKey: 'completionTokens',
    header: 'Completion Tokens',
    cell: ({ row }) => <div>{row.getValue('completionTokens')}</div>
  }
];

const LlmUsage = ({
  llmId,
  fromDate,
  toDate
}: {
  llmId: string;
  fromDate: Date | undefined;
  toDate: Date | undefined;
}) => {
  const {
    fetchAsync,
    data: usageData,
    isFetching: isFetchingUsage,
    error: usageError
  } = useGetLlmUsage(llmId, 'identity_name');
  const { t } = useTranslation();
  const [sorting, setSorting] = useState<SortingState>([
    {
      id: 'completions',
      desc: true
    }
  ]);
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
    manualPagination: true,
    manualSorting: true
  });

  const fetchUsageAsync = useCallback(async () => {
    fetchAsync(
      `orderBy=${sorting.length > 0 ? sorting[0].id : 'completions'}&orderDirection=${sorting.length > 0 ? sorting[0].desc : 'desc'}&from=${fromDate?.toISOString() ?? ''}&to=${toDate?.toISOString() ?? ''}`
    );
  }, [fetchAsync, fromDate, toDate, sorting]);

  useEffect(() => {
    fetchUsageAsync();
  }, [fetchUsageAsync]);

  return (
    <div className="rounded-md border flex flex-col min-h-100 w-full h-full overflow-auto ">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h2 className="text-lg font-medium">{t('LLM Usage')}</h2>
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

export default LlmUsage;
