import * as React from 'react';
import {
  type ColumnDef,
  flexRender,
  getCoreRowModel,
  type PaginationState,
  type RowSelectionState,
  useReactTable
} from '@tanstack/react-table';
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronsLeftIcon,
  ChevronsRightIcon,
  Plus,
  RefreshCcw,
  Trash
} from 'lucide-react';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { type QueryOptions } from 'odata-query';
import { type QueryResult } from '@/hooks/data/core';
import { Input } from '@/components/ui/input';
import { useDebounce } from 'use-debounce';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { LoadingOverlay } from './loading-overlay';
import { type HTMLProps } from 'react';
import { cn } from '@/lib/utils';
import { ConfirmDialog } from './confirm-dialog';
import { useTranslation } from 'react-i18next';

export interface DataTableProps<TData> {
  //idAccessor?: string;
  data?: QueryResult<TData>;
  columns: ColumnDef<TData>[];
  onRowSelectionChange?: (selectedItems: TData[]) => void;
  onQueryChanged?: (query: Partial<QueryOptions<TData>>) => void;
  pageSize?: number;
  pageSizeOptions?: number[];
  leftOptions?: React.ReactNode[];
  rightOptions?: React.ReactNode[];
  header?: React.ReactNode;
  reload?: () => void;
  loading?: boolean;
  enableSelection?: boolean;
  onDeleteSelection?: (selectedItems: TData[]) => void;
  onCreateClick?: () => void;
  getRowClassName?: (row: TData) => string;
  simplePagination?: boolean;
  rowIsSelectable?: (row: TData) => boolean;
  className?: string;
  hideSearch?: boolean;
}

function IndeterminateCheckbox({
  indeterminate,
  className = '',
  ...rest
}: { indeterminate?: boolean } & HTMLProps<HTMLInputElement>) {
  const ref = React.useRef<HTMLInputElement>(null!);

  React.useEffect(() => {
    if (typeof indeterminate === 'boolean') {
      ref.current.indeterminate = !rest.checked && indeterminate;
    }
  }, [ref, indeterminate]);

  return <input type="checkbox" ref={ref} className={className + ' cursor-pointer'} {...rest} />;
}

export function DataTable({
  data,
  columns,
  onRowSelectionChange,
  onQueryChanged,
  pageSize = 10,
  pageSizeOptions = [10, 20, 30, 40, 50],
  leftOptions,
  header,
  rightOptions,
  reload,
  loading,
  enableSelection = false,
  onDeleteSelection,
  onCreateClick,
  getRowClassName,
  simplePagination = false,
  rowIsSelectable,
  className,
  hideSearch = false
}: DataTableProps<any>) {
  const { t } = useTranslation();
  const [rowSelection, setRowSelection] = React.useState<RowSelectionState>({});
  const [pagination, setPagination] = React.useState<PaginationState>({
    pageIndex: 0,
    pageSize: pageSize
  });
  const [searchQuery, setSearchQuery] = React.useState('');
  const [debouncedSearchQuery] = useDebounce(searchQuery, 1000);

  React.useEffect(() => {
    if (pagination && onQueryChanged) {
      const query: Partial<QueryOptions<any>> = {
        top: pagination.pageSize,
        skip: pagination.pageIndex * pagination.pageSize,
        search: debouncedSearchQuery
      };
      onQueryChanged(query);
    }
  }, [pagination, debouncedSearchQuery]);

  React.useEffect(() => {
    if (rowSelection && onRowSelectionChange && data?.items) {
      let selectedItems: any[] = [];
      selectedItems = data.items.filter((_, index) => {
        const row = table.getRowModel().rows[index];
        if (row) {
          return rowSelection[row.id];
        }
      });
      onRowSelectionChange(selectedItems);
    }
  }, [rowSelection]);

  const onDelete = () => {
    if (onDeleteSelection && rowSelection && data?.items) {
      let selectedItems: any[] = [];
      selectedItems = data.items.filter((_, index) => {
        const row = table.getRowModel().rows[index];
        if (row) {
          return rowSelection[row.id];
        }
      });
      onDeleteSelection(selectedItems);
      setRowSelection({});
    }
  };

  const table = useReactTable({
    data: data?.items ?? [],
    columns,
    rowCount: data?.count,
    state: {
      rowSelection,
      pagination
    },
    manualPagination: true,
    enableRowSelection: (row) => {
      return !rowIsSelectable ? true : rowIsSelectable(row.original);
    },
    onRowSelectionChange: setRowSelection,
    onPaginationChange: setPagination,
    getCoreRowModel: getCoreRowModel()
  });

  const simplePaginationCanPreviousPage = () => {
    return pagination.pageIndex > 0;
  };

  const simplePaginationCanNextPage = () => {
    return (data?.items?.length ?? 0) >= pagination.pageSize;
  };

  return (
    <div className={cn(className, 'flex flex-col gap-4')}>
      <div className={'flex flex-wrap justify-between items-end gap-4'}>
        {leftOptions && leftOptions.length > 0 ? (
          <div className="flex items-center gap-2">
            {leftOptions.map((option, index) => (
              <div key={index}>{option}</div>
            ))}
          </div>
        ) : (
          <div></div>
        )}
        <div className="flex items-center gap-2">
          {rowSelection && Object.keys(rowSelection).length > 0 && (
            <ConfirmDialog
              title={t('Delete selected items')}
              description={t('Are you sure want to delete selected items?')}
              acceptText={t('Delete')}
              acceptVariant="destructive"
              onAccept={() => onDelete()}>
              <div>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button className="size-8" variant="destructive">
                      <Trash className="size-4" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent side="bottom">{t('Delete selection')}</TooltipContent>
                </Tooltip>
              </div>
            </ConfirmDialog>
          )}
          {onCreateClick && (
            <Tooltip key="create-identity">
              <TooltipTrigger asChild>
                <Button className="size-8" onClick={onCreateClick}>
                  <Plus className="size-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">{t('New item')}</TooltipContent>
            </Tooltip>
          )}
          {rightOptions && rightOptions.length > 0 && (
            <div className="flex items-center gap-2">
              {rightOptions.map((option, index) => (
                <div key={index}>{option}</div>
              ))}
            </div>
          )}
          {!hideSearch && onQueryChanged && (
            <Input
              placeholder={t('Filter items by...')}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="h-8 w-[150px] lg:w-[250px]"
            />
          )}
          {reload && (
            <Tooltip>
              <TooltipTrigger asChild>
                <Button className="size-8" variant="outline" onClick={() => reload()}>
                  <RefreshCcw className="size-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent side="bottom">{t('Refresh')}</TooltipContent>
            </Tooltip>
          )}
        </div>
      </div>

      <div className="relative">
        {loading && <LoadingOverlay />}
        <div className="overflow-hidden rounded-lg border">
          {header}
          <Table>
            <TableHeader className="sticky top-0 z-10 bg-muted">
              {table.getHeaderGroups().map((headerGroup) => (
                <TableRow key={headerGroup.id}>
                  {enableSelection && (
                    <TableHead key="select-col" className="p-3">
                      <IndeterminateCheckbox
                        {...{
                          checked: table.getIsAllPageRowsSelected(),
                          indeterminate: table.getIsSomePageRowsSelected(),
                          onChange: table.getToggleAllPageRowsSelectedHandler(),
                          className: 'size-4'
                        }}
                      />
                    </TableHead>
                  )}
                  {headerGroup.headers.map((header) => {
                    return (
                      <TableHead key={header.id} colSpan={header.colSpan} className="p-3">
                        {header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}
                      </TableHead>
                    );
                  })}
                </TableRow>
              ))}
            </TableHeader>
            <TableBody className={cn('', enableSelection ? '**:data-[slot=table-cell]:first:w-8' : '')}>
              {table.getRowModel().rows.map((row) => (
                <TableRow
                  key={row.id}
                  className={cn(
                    getRowClassName ? getRowClassName(row.original) : '',
                    row.getIsSelected() && 'bg-accent'
                  )}>
                  {enableSelection && (
                    <TableCell key="select-col" className="p-3">
                      <IndeterminateCheckbox
                        {...{
                          checked: row.getIsSelected(),
                          indeterminate: row.getIsSomeSelected(),
                          onChange: row.getToggleSelectedHandler(),
                          disabled: !row.getCanSelect(),
                          className: 'size-4'
                        }}
                      />
                    </TableCell>
                  )}
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} className="p-3 whitespace-normal break-all">
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
              {table.getRowModel().rows.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={enableSelection ? columns.length + 1 : columns.length}
                    className="h-24 text-center">
                    {t('No results.')}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </div>

        {/* Pagination */}
        {!simplePagination && (
          <div className="flex items-center justify-between py-4">
            <div className="hidden flex-1 text-sm text-muted-foreground lg:flex">
              {t('Total items')}: {data?.count}
            </div>
            {rowSelection && Object.keys(rowSelection).length > 0 ? (
              <div className="hidden flex-1 text-sm text-muted-foreground lg:flex">
                {table.getFilteredSelectedRowModel().rows.length} {t('of')} {table.getFilteredRowModel().rows.length}{' '}
                {t('row(s) selected.')}
              </div>
            ) : (
              <div></div>
            )}

            <div className="flex w-full items-center gap-8 lg:w-fit">
              <div className="hidden items-center gap-2 lg:flex">
                <Label htmlFor="rows-per-page" className="text-sm font-medium">
                  {t('Rows per page')}
                </Label>
                <Select
                  value={`${table.getState().pagination.pageSize}`}
                  onValueChange={(value) => {
                    table.setPageSize(Number(value));
                  }}>
                  <SelectTrigger className="w-20" id="rows-per-page">
                    <SelectValue placeholder={table.getState().pagination.pageSize} />
                  </SelectTrigger>
                  <SelectContent side="top">
                    {pageSizeOptions.map((pageSize) => (
                      <SelectItem key={pageSize} value={`${pageSize}`}>
                        {pageSize}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="flex w-fit items-center justify-center text-sm font-medium">
                {t('Page')} {table.getState().pagination.pageIndex + 1} {t('of')} {table.getPageCount()}
              </div>

              <div className="ml-auto flex items-center gap-2 lg:ml-0">
                <Button
                  variant="outline"
                  className="hidden h-8 w-8 p-0 lg:flex"
                  onClick={() => table.firstPage()}
                  disabled={!table.getCanPreviousPage()}>
                  <span className="sr-only">{t('Go to first page')}</span>
                  <ChevronsLeftIcon />
                </Button>
                <Button
                  variant="outline"
                  className="size-8"
                  size="icon"
                  onClick={() => table.previousPage()}
                  disabled={!table.getCanPreviousPage()}>
                  <span className="sr-only">{t('Go to previous page')}</span>
                  <ChevronLeftIcon />
                </Button>
                <Button
                  variant="outline"
                  className="size-8"
                  size="icon"
                  onClick={() => table.nextPage()}
                  disabled={!table.getCanNextPage()}>
                  <span className="sr-only">{t('Go to next page')}</span>
                  <ChevronRightIcon />
                </Button>
                <Button
                  variant="outline"
                  className="hidden size-8 lg:flex"
                  size="icon"
                  onClick={() => table.lastPage()}
                  disabled={!table.getCanNextPage()}>
                  <span className="sr-only">{t('Go to last page')}</span>
                  <ChevronsRightIcon />
                </Button>
              </div>
            </div>
          </div>
        )}
        {simplePagination && (
          <div className="flex items-center justify-between py-4">
            {rowSelection && Object.keys(rowSelection).length > 0 ? (
              <div className="hidden flex-1 text-sm text-muted-foreground lg:flex">
                {table.getFilteredSelectedRowModel().rows.length} {t('of')} {table.getFilteredRowModel().rows.length}{' '}
                {t('row(s) selected.')}
              </div>
            ) : (
              <div></div>
            )}

            <div className="flex w-full items-center gap-8 lg:w-fit">
              <div className="ml-auto flex items-center gap-2 lg:ml-0">
                <Button
                  variant="outline"
                  className="size-8"
                  size="icon"
                  onClick={() => table.previousPage()}
                  disabled={!simplePaginationCanPreviousPage()}>
                  <span className="sr-only">{t('Go to previous page')}</span>
                  <ChevronLeftIcon />
                </Button>
                <Button
                  variant="outline"
                  className="size-8"
                  size="icon"
                  onClick={() => table.nextPage()}
                  disabled={!simplePaginationCanNextPage()}>
                  <span className="sr-only">{t('Go to next page')}</span>
                  <ChevronRightIcon />
                </Button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
