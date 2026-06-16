import TokensUsageChart from '../charts/tokens-usage';
import { DateTimeRangePicker } from '@/components/date-time-range-picker';
import { useCallback, useMemo, useState } from 'react';
import { subHours } from 'date-fns';
import { useTranslation } from 'react-i18next';
import LlmUsage from '../charts/usage-summary';
import LlmKpiCards from '../charts/llm-kpi-cards';
import ModelSpends from '../charts/model-spends';
import IdentityPicker from '@/pickers/identity-picker';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue
} from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { X } from 'lucide-react';
import { useDebounce } from '@/hooks/use-debounce';
import type { LlmItem, LlmUsageSummaryItem } from '@/hooks/data/llms';

const ALL_MODELS = '__all__';

const MonitorPanel = ({ llm }: { llm: LlmItem }) => {
  const { t } = useTranslation();
  const llmId = llm.id;

  const [fromDate, setFromDate] = useState<Date | undefined>(() => subHours(new Date(), 24));
  const [toDate, setToDate] = useState<Date | undefined>(() => new Date());
  const [identityName, setIdentityName] = useState<string>('');
  const [userIdInput, setUserIdInput] = useState<string>('');
  const [modelSelection, setModelSelection] = useState<string>(ALL_MODELS);
  const [usageRows, setUsageRows] = useState<LlmUsageSummaryItem[] | undefined>(undefined);
  const [usageError, setUsageError] = useState<string | undefined>(undefined);
  const [modelRows, setModelRows] = useState<LlmUsageSummaryItem[]>([]);

  const debouncedUserId = useDebounce(userIdInput, 300);
  // Clearing the input should propagate immediately so "Clear filters" feels instant;
  // typing still waits for the debounce.
  const userId = userIdInput === '' ? '' : debouncedUserId;
  const model = modelSelection === ALL_MODELS ? '' : modelSelection;

  const configuredModels = useMemo(() => {
    const set = new Set<string>();
    for (const d of llm.destination ?? []) {
      if (d?.model) set.add(d.model);
    }
    return Array.from(set).sort();
  }, [llm.destination]);

  const modelOptions = useMemo(() => {
    const set = new Set<string>(configuredModels);
    for (const r of modelRows) {
      if (r.name) set.add(r.name);
    }
    return Array.from(set).sort();
  }, [configuredModels, modelRows]);

  const clearFilters = useCallback(() => {
    setIdentityName('');
    setUserIdInput('');
    setModelSelection(ALL_MODELS);
  }, []);

  const hasFilters = identityName !== '' || userIdInput !== '' || modelSelection !== ALL_MODELS;

  return (
    <div className="flex flex-col w-full gap-4 h-full overflow-auto p-1">
      <div className="flex flex-wrap gap-3 items-center justify-end">
        <DateTimeRangePicker
          initialDateFrom={fromDate}
          initialDateTo={toDate}
          onUpdate={(o) => {
            setFromDate(o.range.from);
            setToDate(o.range.to);
          }}
          className="sm:w-100"
        />
        <IdentityPicker
          className="w-64"
          value={identityName}
          onChange={(v) => setIdentityName(v ?? '')}
          clearable
        />
        <Input
          className="w-48"
          placeholder={t('Filter by user id')}
          value={userIdInput}
          onChange={(e) => setUserIdInput(e.target.value)}
        />
        <Select value={modelSelection} onValueChange={setModelSelection}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder={t('All models')} />
          </SelectTrigger>
          <SelectContent>
            <SelectGroup>
              <SelectItem value={ALL_MODELS}>{t('All models')}</SelectItem>
              {modelOptions.map((m) => (
                <SelectItem key={m} value={m}>
                  {m}
                </SelectItem>
              ))}
            </SelectGroup>
          </SelectContent>
        </Select>
        {hasFilters && (
          <Button variant="ghost" size="sm" onClick={clearFilters}>
            <X /> {t('Clear filters')}
          </Button>
        )}
      </div>

      <LlmKpiCards usage={usageRows} error={usageError} />

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <TokensUsageChart
          llmId={llmId}
          metricName="prompt_tokens"
          title={t('Prompt Tokens Usage')}
          fromDate={fromDate}
          toDate={toDate}
          identityName={identityName}
          userId={userId}
          model={model}
        />
        <TokensUsageChart
          llmId={llmId}
          metricName="completion_tokens"
          title={t('Completion Tokens Usage')}
          fromDate={fromDate}
          toDate={toDate}
          identityName={identityName}
          userId={userId}
          model={model}
        />
        <TokensUsageChart
          llmId={llmId}
          metricName="completion_count"
          title={t('Completions')}
          fromDate={fromDate}
          toDate={toDate}
          identityName={identityName}
          userId={userId}
          model={model}
        />
        <TokensUsageChart
          llmId={llmId}
          metricName="requests_per_minute"
          title={t('Requests Per Minute')}
          fromDate={fromDate}
          toDate={toDate}
          identityName={identityName}
          userId={userId}
          model={model}
        />
        <TokensUsageChart
          llmId={llmId}
          metricName="tokens_per_minute"
          title={t('Tokens Per Minute')}
          fromDate={fromDate}
          toDate={toDate}
          identityName={identityName}
          userId={userId}
          model={model}
        />
        <ModelSpends
          llmId={llmId}
          fromDate={fromDate}
          toDate={toDate}
          identityName={identityName}
          userId={userId}
          model={model}
          onData={setModelRows}
        />
      </div>
      <LlmUsage
        llmId={llmId}
        fromDate={fromDate}
        toDate={toDate}
        identityName={identityName}
        userId={userId}
        model={model}
        onData={setUsageRows}
        onError={setUsageError}
      />
    </div>
  );
};

export default MonitorPanel;
