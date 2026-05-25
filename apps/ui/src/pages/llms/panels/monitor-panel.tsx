import TokensUsageChart from '../charts/tokens-usage';
import { DateTimeRangePicker } from '@/components/date-time-range-picker';
import { useState } from 'react';
import { subHours } from 'date-fns';
import { useTranslation } from 'react-i18next';
import LlmUsage from '../charts/usage-summary';

const MonitorPanel = ({ llmId }: { llmId: string }) => {
  const { t } = useTranslation();

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
        <TokensUsageChart
          llmId={llmId}
          metricName="prompt_tokens"
          title={t('Prompt Tokens Usage')}
          fromDate={fromDate}
          toDate={toDate}
        />
        <TokensUsageChart
          llmId={llmId}
          metricName="completion_tokens"
          title={t('Completion Tokens Usage')}
          fromDate={fromDate}
          toDate={toDate}
        />
        <TokensUsageChart
          llmId={llmId}
          metricName="completion_count"
          title={t('Completions')}
          fromDate={fromDate}
          toDate={toDate}
        />
      </div>
      <LlmUsage llmId={llmId} fromDate={fromDate} toDate={toDate} />
    </div>
  );
};

export default MonitorPanel;
