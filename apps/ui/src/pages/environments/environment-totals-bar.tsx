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
  quotaDisplay
}: {
  label: string;
  resource?: ResourceTotal;
  format: Format;
  quotaDisplay: string;
}) {
  const { t } = useTranslation();
  const used = resource?.used ?? 0;
  const requested = resource?.requested ?? 0;
  const allocated = resource?.allocated ?? 0;
  const quota = resource?.quota;

  // Scale relative to quota when known, otherwise to the largest tracked value.
  const denominator = quota && quota > 0 ? quota : Math.max(allocated, requested, used, 1);
  const pct = (value: number) => Math.min(100, (value / denominator) * 100);

  const hasQuota = !!quota && quota > 0;
  // Percentages in the tooltip are always relative to quota. When no quota is set,
  // a percentage is meaningless (the bar self-scales), so we omit the suffix.
  const pctSuffix = (value: number) => (hasQuota ? ` (${formatPercent((value / quota!) * 100)})` : '');
  // Over-allocation is the cluster-meaningful overflow: Allocated > Quota means
  // the env *could* exceed its ceiling if every app started. Highlight gray in amber.
  const isOverAllocated = hasQuota && allocated > quota!;
  const allocatedBarClass = isOverAllocated
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
                <span className="text-muted-foreground"> / {quotaDisplay}</span>
              </span>
            </div>
            <div
              role="progressbar"
              aria-label={label}
              aria-valuenow={used}
              aria-valuemin={0}
              aria-valuemax={quota ?? Math.max(allocated, requested, used)}
              className="relative h-1.5 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
              {/* Back-to-front: allocated (widest, gray) → requested (blue) → used (green). */}
              <div
                className={`absolute inset-y-0 left-0 ${allocatedBarClass}`}
                style={{ width: `${pct(allocated)}%` }}
              />
              <div
                className="absolute inset-y-0 left-0 bg-blue-500"
                style={{ width: `${pct(requested)}%` }}
              />
              <div
                className="absolute inset-y-0 left-0 bg-emerald-500"
                style={{ width: `${pct(used)}%` }}
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
              <span className="text-blue-400">●</span> {t('Requested')}: {formatValue(requested, format)}
              {pctSuffix(requested)}
            </div>
            <div>
              <span className={isOverAllocated ? 'text-amber-400' : 'text-gray-400'}>●</span>{' '}
              {t('Allocated')}: {formatValue(allocated, format)}
              {pctSuffix(allocated)}
            </div>
            <div className="text-muted-foreground">
              {t('Quota')}: {quotaDisplay}
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
  cpuQuotaDisplay,
  memoryQuotaDisplay
}: {
  cpu?: ResourceTotal;
  memory?: ResourceTotal;
  cpuQuotaDisplay: string;
  memoryQuotaDisplay: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-row gap-6 items-center">
      <ResourceUsageBar label={t('CPU')} resource={cpu} format="cores" quotaDisplay={cpuQuotaDisplay} />
      <ResourceUsageBar label={t('MEM')} resource={memory} format="bytes" quotaDisplay={memoryQuotaDisplay} />
    </div>
  );
}
