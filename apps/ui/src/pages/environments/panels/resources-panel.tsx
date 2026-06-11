import { Empty, EmptyHeader, EmptyMedia, EmptyTitle, EmptyDescription } from '@/components/ui/empty';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import TagBadges from '@/components/tag-badges';
import type { EnvironmentItem, ResourceSize } from '@/hooks/data/environments';
import { Boxes, HardDrive } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// Show the request value, and append the limit when it differs (e.g. "2 / 4").
function ResourceValue({ requests, limits, field }: { requests?: ResourceSize; limits?: ResourceSize; field: keyof ResourceSize }) {
  const req = requests?.[field];
  const lim = limits?.[field];
  if (!req && !lim) return <span className="text-muted-foreground">—</span>;
  if (lim && lim !== req) {
    return (
      <span>
        {req || '—'} <span className="text-muted-foreground">/ {lim}</span>
      </span>
    );
  }
  return <span>{req || lim}</span>;
}

function basename(path: string): string {
  const trimmed = path.replace(/\/+$/, '');
  const idx = trimmed.lastIndexOf('/');
  return idx >= 0 ? trimmed.slice(idx + 1) || trimmed : trimmed;
}

const EnvironmentResourcesPanel = ({ environment }: { environment: EnvironmentItem }) => {
  const { t } = useTranslation();
  const sizes = environment.definition?.sizes ?? [];
  const hostPaths = environment.definition?.hostPaths ?? [];

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2">
        <h3 className="text-sm text-muted-foreground flex-1 min-w-0 truncate">
          {t('Application sizes and host paths defined by this environment (read-only)')}
        </h3>
      </div>
      <div className="flex flex-col gap-8 p-2 h-full overflow-auto">
        {/* Application Sizes */}
        <section className="flex flex-col gap-2">
          <h4 className="text-sm font-medium text-muted-foreground">{t('Application sizes')}</h4>
          {sizes.length === 0 ? (
            <Empty>
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <Boxes />
                </EmptyMedia>
                <EmptyTitle>{t('No application sizes defined')}</EmptyTitle>
                <EmptyDescription>{t('This environment does not expose any application size presets.')}</EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : (
            <div className="overflow-hidden rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('Name')}</TableHead>
                    <TableHead>{t('Title')}</TableHead>
                    <TableHead>{t('CPU')}</TableHead>
                    <TableHead>{t('Memory')}</TableHead>
                    <TableHead>{t('GPU')}</TableHead>
                    <TableHead>{t('Storage')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sizes.map((size) => (
                    <TableRow key={size.name}>
                      <TableCell className="font-mono text-xs">{size.name}</TableCell>
                      <TableCell>{size.title}</TableCell>
                      <TableCell>
                        <ResourceValue requests={size.requests} limits={size.limits} field="cpu" />
                      </TableCell>
                      <TableCell>
                        <ResourceValue requests={size.requests} limits={size.limits} field="memory" />
                      </TableCell>
                      <TableCell>
                        <ResourceValue requests={size.requests} limits={size.limits} field="gpu" />
                      </TableCell>
                      <TableCell>
                        <ResourceValue requests={size.requests} limits={size.limits} field="storage" />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </section>

        {/* Host Paths */}
        <section className="flex flex-col gap-2">
          <h4 className="text-sm font-medium text-muted-foreground">{t('Host paths')}</h4>
          {hostPaths.length === 0 ? (
            <Empty>
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <HardDrive />
                </EmptyMedia>
                <EmptyTitle>{t('No host paths defined')}</EmptyTitle>
                <EmptyDescription>{t('This environment does not expose any host path mounts.')}</EmptyDescription>
              </EmptyHeader>
            </Empty>
          ) : (
            <div className="overflow-hidden rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('Title')}</TableHead>
                    <TableHead>{t('Path')}</TableHead>
                    <TableHead>{t('Allowed')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {hostPaths.map((hp) => (
                    <TableRow key={hp.path}>
                      <TableCell>{hp.title || basename(hp.path)}</TableCell>
                      <TableCell className="font-mono text-xs">{hp.path}</TableCell>
                      <TableCell>
                        {hp.allowed && hp.allowed.length > 0 ? (
                          <TagBadges tags={hp.allowed} max={hp.allowed.length} />
                        ) : (
                          <span className="text-muted-foreground text-xs">{t('No one')}</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </section>
      </div>
    </div>
  );
};

export default EnvironmentResourcesPanel;
