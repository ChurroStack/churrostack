import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { formatBytes } from '@/extensions';
import type { ResourceTotal } from '@/hooks/data/environments';
import { useTranslation } from 'react-i18next';

type Format = 'cores' | 'bytes';

function formatValue(value: number, format: Format) {
  if (format === 'bytes') return formatBytes(value);
  // Cores: show up to 2 decimals for fractional cores, drop trailing zeros.
  return Number.isInteger(value) ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
}

function formatPercent(pct: number) {
  if (pct >= 1 || pct === 0) return `${Math.round(pct)}%`;
  // Sub-1% values like 0.06/16 would round to 0% — keep one decimal so they stay visible.
  return `${pct.toFixed(1)}%`;
}

function ResourceUsageBar({
  label,
  resource,
  format,
  totalDisplay
}: {
  label: string;
  resource?: ResourceTotal;
  format: Format;
  totalDisplay: string;
}) {
  const { t } = useTranslation();
  const used = resource?.used ?? 0;
  const requested = resource?.requested ?? 0;
  const total = resource?.total;
  // Scale relative to total when known, otherwise to the larger of (requested, used).
  const denominator = total && total > 0 ? total : Math.max(requested, used, 1);
  const usedPct = Math.min(100, (used / denominator) * 100);
  // Requested is drawn behind used; clamp so the gray segment never exceeds the track.
  const requestedPct = Math.min(100, (Math.max(requested, used) / denominator) * 100);
  // Percentages in the tooltip are always relative to total. When no quota is set,
  // a percentage is meaningless (the bar self-scales), so we omit the suffix.
  const hasTotal = !!total && total > 0;
  const pctSuffix = (value: number) => (hasTotal ? ` (${formatPercent((value / total!) * 100)})` : '');
  const isOverAllocated = hasTotal && requested > total!;
  const requestedBarClass = isOverAllocated
    ? 'bg-amber-300 dark:bg-amber-600'
    : 'bg-gray-300 dark:bg-gray-600';

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div className="flex flex-col gap-1 min-w-32">
            <div className="flex flex-row items-baseline gap-1">
              <span className="text-xs text-muted-foreground">{label}</span>
              <span className="font-mono text-xs text-gray-700 dark:text-gray-300 ml-auto">
                {formatValue(used, format)}
                <span className="text-muted-foreground"> / {totalDisplay}</span>
              </span>
            </div>
            <div
              role="progressbar"
              aria-label={label}
              aria-valuenow={used}
              aria-valuemin={0}
              aria-valuemax={total ?? Math.max(requested, used)}
              className="relative h-1.5 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
              <div
                className={`absolute inset-y-0 left-0 ${requestedBarClass}`}
                style={{ width: `${requestedPct}%` }}
              />
              <div
                className="absolute inset-y-0 left-0 bg-emerald-500"
                style={{ width: `${usedPct}%` }}
              />
            </div>
          </div>
        </TooltipTrigger>
        <TooltipContent className="text-xs">
          <div className="flex flex-col gap-0.5">
            <div>
              <span className="text-emerald-400">●</span> {t('Used')}: {formatValue(used, format)}
              {pctSuffix(used)}
            </div>
            <div>
              <span className={isOverAllocated ? 'text-amber-400' : 'text-gray-400'}>●</span> {t('Requested')}:{' '}
              {formatValue(requested, format)}
              {pctSuffix(requested)}
            </div>
            <div className="text-muted-foreground">
              {t('Total')}: {totalDisplay}
            </div>
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

export function EnvironmentTotalsBar({
  cpu,
  memory,
  cpuTotalDisplay,
  memoryTotalDisplay
}: {
  cpu?: ResourceTotal;
  memory?: ResourceTotal;
  cpuTotalDisplay: string;
  memoryTotalDisplay: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-row gap-6 items-center">
      <ResourceUsageBar label={t('CPU')} resource={cpu} format="cores" totalDisplay={cpuTotalDisplay} />
      <ResourceUsageBar label={t('MEM')} resource={memory} format="bytes" totalDisplay={memoryTotalDisplay} />
    </div>
  );
}
