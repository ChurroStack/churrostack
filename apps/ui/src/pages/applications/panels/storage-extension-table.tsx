import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow
} from '@/components/ui/table';
import { renderIcon } from '@/extensions';
import type { ApplicationExtensionItem, ApplicationItem } from '@/hooks/data/applications';
import { useGetEnvironmentHostPaths } from '@/hooks/data/environments';
import { useGetTemplate } from '@/hooks/data/templates';
import { Plus, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

const NO_MAPPING = '__none__';

interface StorageRow {
  rid: number; // stable React key, independent of the persisted extension name
  name: string;
  path: string;
  size: string;
  hostPath: string;
}

/**
 * Renders all storage extensions of an application as an editable table — one row
 * per `com.churrostack.extension.storage` entry. Each row maps to a mount path, a
 * size, and an optional environment-managed host path ("Map to"). Emits the full
 * set of storage extensions to the parent whenever a row changes.
 */
const StorageExtensionTable = ({
  app,
  baseName,
  extensionTemplate,
  environmentType,
  environmentName,
  onChange
}: {
  app?: ApplicationItem;
  baseName: string;
  extensionTemplate: string;
  environmentType: string;
  environmentName: string;
  onChange: (extensions: ApplicationExtensionItem[]) => void;
}) => {
  const { t } = useTranslation();
  const { fetchAsync: fetchTemplate, data: template } = useGetTemplate(`${extensionTemplate}:${environmentType}`);
  const { fetchAsync: fetchHostPaths, data: hostPaths } = useGetEnvironmentHostPaths(environmentName);
  const [rows, setRows] = useState<StorageRow[]>([]);
  const ridCounter = useRef(0);

  const sizeOptions = template?.definition?.parameters?.size?.options ?? [
    { title: '1Gi', value: '1Gi' }
  ];
  const defaultSize = template?.definition?.parameters?.size?.default_value?.[0] ?? sizeOptions[0]?.value ?? '1Gi';
  const defaultPath = template?.definition?.parameters?.path?.default_value?.[0] ?? '/app/home';

  const isStorageExtension = (name: string) => name === baseName || name.startsWith(`${baseName}-`);

  useEffect(() => {
    fetchTemplate('');
  }, [extensionTemplate, environmentType]);

  useEffect(() => {
    if (environmentName) {
      fetchHostPaths('');
    }
  }, [environmentName]);

  // Seed rows from the persisted storage extensions.
  useEffect(() => {
    const stored = (app?.extensions ?? []).filter((ext) => isStorageExtension(ext.name));
    setRows(
      stored.map((ext) => ({
        rid: ridCounter.current++,
        name: ext.name,
        path: ext.parameters?.path?.[0] ?? defaultPath,
        size: ext.parameters?.size?.[0] ?? defaultSize,
        hostPath: ext.parameters?.hostPath?.[0] ?? ''
      }))
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [app, baseName]);

  const hostPathOptions = useMemo(() => hostPaths ?? [], [hostPaths]);
  const hasHostPaths = hostPathOptions.length > 0;

  const nextName = (existing: StorageRow[]) => {
    const taken = new Set(existing.map((r) => r.name));
    if (!taken.has(baseName)) return baseName;
    let i = 2;
    while (taken.has(`${baseName}-${i}`)) i++;
    return `${baseName}-${i}`;
  };

  const emit = (next: StorageRow[]) => {
    setRows(next);
    onChange(
      next.map((r) => {
        const parameters: { [key: string]: string[] } = {
          path: [r.path],
          size: [r.size]
        };
        if (r.hostPath) {
          parameters.hostPath = [r.hostPath];
        }
        return {
          name: r.name,
          enabled: true,
          templateName: extensionTemplate,
          parameters
        };
      })
    );
  };

  const updateRow = (rid: number, patch: Partial<StorageRow>) =>
    emit(rows.map((r) => (r.rid === rid ? { ...r, ...patch } : r)));

  const addRow = () =>
    emit([
      ...rows,
      { rid: ridCounter.current++, name: nextName(rows), path: defaultPath, size: defaultSize, hostPath: '' }
    ]);

  const removeRow = (rid: number) => emit(rows.filter((r) => r.rid !== rid));

  return (
    <Card className="shadow-none p-4 gap-2">
      <CardHeader className="px-0">
        <div className="flex flex-row items-center gap-2 w-full">
          {renderIcon(template?.definition?.icon ?? 'lucide:hard-drive', 'size-4')}
          <span className="uppercase text-sm font-semibold">{template?.title ?? baseName}</span>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Mount path')}</TableHead>
              <TableHead className="w-40">{t('Size')}</TableHead>
              <TableHead className="w-60">{t('Map to')}</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => {
              // Preserve a previously-saved host path even if it is no longer offered.
              const options = row.hostPath && !hostPathOptions.some((o) => o.path === row.hostPath)
                ? [{ path: row.hostPath, title: row.hostPath }, ...hostPathOptions]
                : hostPathOptions;
              const mapDisabled = !hasHostPaths && !row.hostPath;
              return (
                <TableRow key={row.rid}>
                  <TableCell>
                    <Input
                      value={row.path}
                      placeholder={defaultPath}
                      onChange={(e) => updateRow(row.rid, { path: e.target.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <Select value={row.size} onValueChange={(v) => updateRow(row.rid, { size: v })}>
                      <SelectTrigger className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {sizeOptions.map((o) => (
                          <SelectItem key={o.value} value={o.value}>
                            {o.title}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Select
                      value={row.hostPath ? row.hostPath : NO_MAPPING}
                      disabled={mapDisabled}
                      onValueChange={(v) => updateRow(row.rid, { hostPath: v === NO_MAPPING ? '' : v })}>
                      <SelectTrigger className="w-full">
                        <SelectValue placeholder={mapDisabled ? t('Not available') : t('None')} />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={NO_MAPPING}>{t('None')}</SelectItem>
                        {options.map((o) => (
                          <SelectItem key={o.path} value={o.path}>
                            {o.title ?? o.path}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={(e) => {
                        e.preventDefault();
                        removeRow(row.rid);
                      }}>
                      <Trash2 />
                    </Button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
        <Button variant="ghost" className="mt-2" onClick={addRow}>
          <Plus /> {t('Add storage')}
        </Button>
      </CardContent>
    </Card>
  );
};

export default StorageExtensionTable;
