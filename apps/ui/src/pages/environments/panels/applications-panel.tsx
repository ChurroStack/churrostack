import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { formatBytes, formatDistanceToNow, formatPercent } from '@/extensions';
import { getApplicationStatus, useGetApplications } from '@/hooks/data/applications';
import { AppStatus } from '@/pages/applications/common/app-status';
import { AppWindow, Cpu, MemoryStick } from 'lucide-react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

function EnvironmentsApplicationsPanel({ environmentName }: { environmentName: string }) {
  const { t } = useTranslation();
  const { fetchAsync: fetchApplications, data: apps } = useGetApplications();

  useEffect(() => {
    fetchApplications(`environment=${environmentName}`);
  }, [environmentName, fetchApplications]);

  return (
    <div className="p-4 h-full overflow-auto bg-[radial-gradient(circle,_rgba(0,0,0,0.15)_1px,_transparent_1px)] dark:bg-[radial-gradient(circle,_rgba(255,255,255,0.15)_1px,_transparent_1px)] bg-[length:20px_20px]">
      {apps && apps.items && (
        <div
          className="
          w-full
          grid
          grid-cols-1
          sm:grid-cols-2
          lg:grid-cols-3
          xl:grid-cols-4
          gap-4">
          {apps.items.map((application) => {
            const cpuPct = application.metrics?.cpu_usage_pct;
            const memPct = application.metrics?.memory_usage_pct;
            const cpuLimit = application.metrics?.cpu_limit;
            const memLimit = application.metrics?.memory_limit;
            const peakPct = Math.max(cpuPct ?? 0, memPct ?? 0);
            const cpuOfLimit =
              cpuLimit !== undefined
                ? cpuLimit < 1
                  ? `${Math.round(cpuLimit * 1000)}m`
                  : Number.isInteger(cpuLimit)
                    ? `${cpuLimit} ${cpuLimit === 1 ? t('core') : t('cores')}`
                    : `${cpuLimit.toFixed(2)} ${t('cores')}`
                : t('limit');
            const memOfLimit = memLimit !== undefined ? formatBytes(memLimit) : t('limit');
            const cardClass =
              peakPct >= 0.9
                ? 'bg-red-50 border-red-500 dark:bg-red-950/40 dark:border-red-500'
                : peakPct >= 0.7
                  ? 'bg-orange-50 border-orange-500 dark:bg-orange-950/40 dark:border-orange-500'
                  : 'bg-white border dark:bg-gray-800 dark:border-gray-700';
            return (
              <div
                className={`flex flex-col shadow-sm rounded-md w-full cursor-pointer min-w-60 border ${cardClass}`}
                key={application.name}>
                <Link
                  to={`/applications/${application.name}`}
                  key={application.name}
                  className="hover:bg-sidebar-accent hover:text-sidebar-accent-foreground flex flex-col items-start gap-2 border-b p-4 text-sm leading-tight last:border-b-0">
                  <div className="flex w-full justify-between items-center">
                    <span className="font-medium flex flex-row items-center gap-2 cursor-pointer">
                      <AppWindow size={16} />{' '}
                      <span className="w-min-0 break-all max-w-55 truncate cursor-pointer">{application.name}</span>
                    </span>
                  </div>
                  <div className="flex flex-row gap-4 justify-start w-full items-center text-muted-foreground text-xs">
                    <Tooltip>
                      <TooltipTrigger>
                        <div className="flex flex-row gap-1">
                          <Cpu className="size-4" /> {cpuPct !== undefined ? `${formatPercent(cpuPct)}%` : '-'}
                        </div>
                      </TooltipTrigger>
                      <TooltipContent>
                        {cpuPct !== undefined
                          ? `${t('CPU usage')}: ${formatPercent(cpuPct)}% ${t('of')} ${cpuOfLimit}`
                          : t('CPU usage')}
                      </TooltipContent>
                    </Tooltip>
                    <Tooltip>
                      <TooltipTrigger>
                        <div className="flex flex-row gap-1">
                          <MemoryStick className="size-4 rotate-135" />{' '}
                          {formatBytes(parseFloat(`${application.metrics?.memory_usage ?? '0'}`)) ?? '-'}
                        </div>
                      </TooltipTrigger>
                      <TooltipContent>
                        {memPct !== undefined
                          ? `${t('Memory usage')}: ${formatPercent(memPct)}% ${t('of')} ${memOfLimit}`
                          : t('Memory usage (bytes)')}
                      </TooltipContent>
                    </Tooltip>
                    <AppStatus
                      status={getApplicationStatus(application.provisionStatus, application.executionStatus)}
                    />
                  </div>
                  <div className="text-xs break-all">
                    <span>{formatDistanceToNow(application.createdAt)}</span> {t('by')}{' '}
                    <span>{application.createdBy?.name}</span>
                  </div>
                </Link>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default EnvironmentsApplicationsPanel;
