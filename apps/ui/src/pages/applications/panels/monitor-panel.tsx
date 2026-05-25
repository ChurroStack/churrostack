import { parseCpu, parseSize } from '@/extensions';
import MemoryUsageChart from '../charts/memory-usage';
import CpuUsageChart from '../charts/cpu-usage';
import { DateTimeRangePicker } from '@/components/date-time-range-picker';
import { useState } from 'react';
import { subHours } from 'date-fns';
import BytesInChart from '../charts/bytes-in';
import BytesOutChart from '../charts/bytes-out';
import ApplicationUsage from '../charts/usage-summary';

const MonitorPanel = ({ appName, maxMemory, maxCpu }: { appName: string; maxMemory?: string; maxCpu?: string }) => {
  const [fromDate, setFromDate] = useState<Date | undefined>(() => subHours(new Date(), 1));
  const [toDate, setToDate] = useState<Date | undefined>(() => new Date());

  return (
    <div className="flex flex-col w-full gap-2 h-full overflow-auto">
      <div className="flex flex-row gap-2 items-center justify-end">
        <DateTimeRangePicker
          initialDateFrom={fromDate}
          initialDateTo={toDate}
          onUpdate={(o) => {
            setFromDate(o.range.from);
            setToDate(o.range.to);
          }}
          className="sm:w-100"
        />
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <CpuUsageChart
          appName={appName}
          maxValue={!maxCpu ? undefined : parseCpu(maxCpu)}
          fromDate={fromDate}
          toDate={toDate}
        />
        <MemoryUsageChart
          appName={appName}
          maxValue={!maxMemory ? undefined : parseSize(maxMemory)}
          fromDate={fromDate}
          toDate={toDate}
        />
        <BytesInChart appName={appName} maxValue={undefined} fromDate={fromDate} toDate={toDate} />
        <BytesOutChart appName={appName} maxValue={undefined} fromDate={fromDate} toDate={toDate} />
      </div>
      <ApplicationUsage appName={appName} fromDate={fromDate} toDate={toDate} />
    </div>
  );
};

export default MonitorPanel;
