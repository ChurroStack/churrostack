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
  // Effective admission ceiling (quota × overcommit factor). Falls back to quota.
  const ceiling = resource?.ceiling ?? quota;

  const hasQuota = !!quota && quota > 0;
  const hasCeiling = !!ceiling && ceiling > 0;
  // The overcommit band exists when the effective ceiling meaningfully exceeds the base quota.
  const hasOvercommit = hasQuota && hasCeiling && ceiling! > quota! * 1.0001;

  // Scale the track to the effective ceiling when known, else the quota, else self-scale.
  const scaleTo = hasCeiling ? ceiling! : hasQuota ? quota! : Math.max(allocated, requested, used, 1);
  const pct = (value: number) => Math.min(100, (value / scaleTo) * 100);

  // Percentages in the tooltip are relative to the base quota (the number the user knows).
  // When no quota is set, a percentage is meaningless (the bar self-scales), so we omit it.
  const pctSuffix = (value: number) => (hasQuota ? ` (${formatPercent((value / quota!) * 100)})` : '');
  // Real overflow now means exceeding the *effective ceiling* — Allocated between quota and
  // ceiling is expected headroom under overcommit, not an alarm. Highlight gray in amber only
  // when it passes the ceiling.
  const overflowLimit = hasCeiling ? ceiling! : quota;
  const isOverAllocated = !!overflowLimit && allocated > overflowLimit;
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
              aria-valuemax={scaleTo}
              className="relative h-1.5 w-full overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
              {/* Overcommit band: the region between the base quota and the effective ceiling,
                  shown as subtle stripes so the extra headroom reads as "oversubscribed". */}
              {hasOvercommit && (
                <div
                  className="absolute inset-y-0"
                  style={{
                    left: `${pct(quota!)}%`,
                    right: 0,
                    backgroundImage:
                      'repeating-linear-gradient(45deg, rgba(251,191,36,0.35) 0, rgba(251,191,36,0.35) 2px, transparent 2px, transparent 5px)'
                  }}
                />
              )}
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
              {/* Base-quota marker line when an overcommit ceiling extends the track past it. */}
              {hasOvercommit && (
                <div
                  className="absolute inset-y-0 w-px bg-gray-500/70 dark:bg-gray-300/60"
                  style={{ left: `${pct(quota!)}%` }}
                />
              )}
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
            {hasOvercommit && (
              <div className="text-amber-500">
                {t('Overcommit ceiling')}: {formatValue(ceiling!, format)}
                {` (${formatPercent((ceiling! / quota!) * 100)})`}
              </div>
            )}
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
