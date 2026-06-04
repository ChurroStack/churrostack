import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Skeleton } from '@/components/ui/skeleton';
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
import { AlertCircle, ArrowDown, ArrowUp, ArrowUpDown, Receipt, RefreshCcw } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usdFormatter } from './format-usd';

const intFormatter = new Intl.NumberFormat('en-US');

const SortableHeader = ({
  column,
  label
}: {
  column: { toggleSorting: (desc?: boolean) => void; getIsSorted: () => false | 'asc' | 'desc' };
  label: string;
}) => {
  const sorted = column.getIsSorted();
  return (
    <Button
      variant="ghost"
      size="sm"
      className="-ml-2 h-8 px-2"
      onClick={() => column.toggleSorting(sorted === 'asc')}>
      {label}
      {sorted === 'asc' ? (
        <ArrowUp className="ml-1 size-3" />
      ) : sorted === 'desc' ? (
        <ArrowDown className="ml-1 size-3" />
      ) : (
        <ArrowUpDown className="ml-1 size-3 opacity-50" />
      )}
    </Button>
  );
};

const buildColumns = (t: (k: string) => string): ColumnDef<LlmUsageSummaryItem>[] => [
  {
    accessorKey: 'name',
    header: ({ column }) => <SortableHeader column={column} label={t('Name')} />,
    cell: ({ row }) => {
      const name = row.getValue<string>('name');
      return <div>{name && name.length > 0 ? name : <span className="text-muted-foreground italic">{t('(anonymous)')}</span>}</div>;
    }
  },
  {
    accessorKey: 'completions',
    header: ({ column }) => <SortableHeader column={column} label={t('Completions')} />,
    cell: ({ row }) => <div>{intFormatter.format(row.getValue<number>('completions'))}</div>
  },
  {
    accessorKey: 'promptTokens',
    header: ({ column }) => <SortableHeader column={column} label={t('Prompt Tokens')} />,
    cell: ({ row }) => <div>{intFormatter.format(row.getValue<number>('promptTokens'))}</div>
  },
  {
    accessorKey: 'completionTokens',
    header: ({ column }) => <SortableHeader column={column} label={t('Completion Tokens')} />,
    cell: ({ row }) => <div>{intFormatter.format(row.getValue<number>('completionTokens'))}</div>
  },
  {
    accessorKey: 'inputSpend',
    header: ({ column }) => <SortableHeader column={column} label={t('Input Spend')} />,
    cell: ({ row }) => <div>{usdFormatter.format(row.getValue<number>('inputSpend'))}</div>
  },
  {
    accessorKey: 'outputSpend',
    header: ({ column }) => <SortableHeader column={column} label={t('Output Spend')} />,
    cell: ({ row }) => <div>{usdFormatter.format(row.getValue<number>('outputSpend'))}</div>
  },
  {
    accessorKey: 'totalSpend',
    header: ({ column }) => <SortableHeader column={column} label={t('Total Spend')} />,
    cell: ({ row }) => <div className="font-medium">{usdFormatter.format(row.getValue<number>('totalSpend'))}</div>
  }
];

const LlmUsage = ({
  llmId,
  fromDate,
  toDate,
  identityName,
  userId,
  model,
  onData,
  onError
}: {
  llmId?: string;
  fromDate: Date | undefined;
  toDate: Date | undefined;
  identityName?: string;
  userId?: string;
  model?: string;
  onData?: (rows: LlmUsageSummaryItem[] | undefined) => void;
  onError?: (error: string | undefined) => void;
}) => {
  const {
    fetchAsync,
    data: usageData,
    isFetching: isFetchingUsage,
    error: usageError
  } = useGetLlmUsage(llmId, 'identity_name');
  const { t } = useTranslation();
  const [sorting, setSorting] = useState<SortingState>([{ id: 'totalSpend', desc: true }]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({});
  const [rowSelection, setRowSelection] = useState({});

  const columns = useMemo(() => buildColumns(t), [t]);

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
    }
  });

  const fetchUsageAsync = useCallback(async () => {
    const params = new URLSearchParams();
    if (fromDate) params.set('from', fromDate.toISOString());
    if (toDate) params.set('to', toDate.toISOString());
    if (identityName) params.set('identityName', identityName);
    if (userId) params.set('userId', userId);
    if (model) params.set('model', model);
    fetchAsync(params.toString());
  }, [fetchAsync, fromDate, toDate, identityName, userId, model]);

  useEffect(() => {
    fetchUsageAsync();
  }, [fetchUsageAsync]);

  // Propagate undefined too: when the time range / filters change, the hook resets usageData to
  // undefined so the KPI cards (which read this via panel state) fall back to their skeleton
  // instead of showing the previous period's totals.
  useEffect(() => {
    onData?.(usageData?.items);
  }, [usageData, onData]);

  useEffect(() => {
    onError?.(usageError);
  }, [usageError, onError]);

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
          {!usageData
            ? Array.from({ length: 6 }).map((_, i) => (
                <TableRow key={`skel-${i}`}>
                  <TableCell colSpan={columns.length}>
                    <Skeleton className="h-6 w-full" />
                  </TableCell>
                </TableRow>
              ))
            : table.getRowModel().rows?.length
              ? table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id} data-state={row.getIsSelected() && 'selected'}>
                    {row.getVisibleCells().map((cell) => (
                      <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
                    ))}
                  </TableRow>
                ))
              : null}
        </TableBody>
      </Table>
      {usageData && table.getRowModel().rows.length === 0 && (
        <Empty>
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <Receipt />
            </EmptyMedia>
            <EmptyTitle>{t('No usage in this period')}</EmptyTitle>
            <EmptyDescription>{t('Adjust the date range or filters to see results.')}</EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
    </div>
  );
};

export default LlmUsage;
