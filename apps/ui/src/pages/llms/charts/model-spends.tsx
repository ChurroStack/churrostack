import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Skeleton } from '@/components/ui/skeleton';
import { useGetLlmUsage, type LlmUsageSummaryItem } from '@/hooks/data/llms';
import { AlertCircle, ChartColumn } from 'lucide-react';
import { useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Bar, BarChart, CartesianGrid, Tooltip, XAxis, YAxis } from 'recharts';
import { usdFormatter } from './format-usd';

const chartConfig = {
  totalSpend: {
    label: 'Spend',
    color: 'var(--primary)'
  }
} satisfies ChartConfig;

export type ModelSpendsResult = {
  data: LlmUsageSummaryItem[];
  isFetching: boolean;
};

const ModelSpends = ({
  llmId,
  fromDate,
  toDate,
  identityName,
  userId,
  model,
  onData
}: {
  llmId?: string;
  fromDate: Date | undefined;
  toDate: Date | undefined;
  identityName?: string;
  userId?: string;
  model?: string;
  onData?: (rows: LlmUsageSummaryItem[]) => void;
}) => {
  const { t } = useTranslation();
  const { fetchAsync, data, error } = useGetLlmUsage(llmId, 'destination_model');

  const refetch = useCallback(() => {
    const params = new URLSearchParams();
    if (fromDate) params.set('from', fromDate.toISOString());
    if (toDate) params.set('to', toDate.toISOString());
    if (identityName) params.set('identityName', identityName);
    if (userId) params.set('userId', userId);
    if (model) params.set('model', model);
    fetchAsync(params.toString());
  }, [fetchAsync, fromDate, toDate, identityName, userId, model]);

  useEffect(() => {
    refetch();
  }, [refetch]);

  useEffect(() => {
    if (data?.items) {
      onData?.(data.items);
    }
  }, [data, onData]);

  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading model spend data')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  if (!data) {
    return (
      <div className="overflow-hidden rounded-md border flex flex-col gap-4 min-h-0 w-full h-full p-2">
        <h3 className="text-sm font-semibold">{t('Model Spends')}</h3>
        <Skeleton className="h-[200px] w-full" />
      </div>
    );
  }

  const rows = (data?.items ?? []).slice().sort((a, b) => b.totalSpend - a.totalSpend);

  if (rows.length === 0) {
    return (
      <div className="overflow-hidden rounded-md border flex flex-col gap-4 min-h-0 w-full h-full p-2">
        <h3 className="text-sm font-semibold">{t('Model Spends')}</h3>
        <Empty>
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <ChartColumn />
            </EmptyMedia>
            <EmptyTitle>{t('No spend captured yet')}</EmptyTitle>
            <EmptyDescription>{t('Run requests in this period to see spend per model.')}</EmptyDescription>
          </EmptyHeader>
        </Empty>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-md border flex flex-col gap-4 min-h-0 w-full h-full p-2">
      <h3 className="text-sm font-semibold">{t('Model Spends')}</h3>
      <ChartContainer config={chartConfig} title={t('Model Spends')} className="h-[200px] w-full">
        <BarChart accessibilityLayer data={rows} layout="vertical">
          <CartesianGrid horizontal={false} />
          <XAxis type="number" tickFormatter={(v) => usdFormatter.format(v as number)} tick={{ fontSize: 12 }} />
          <YAxis type="category" dataKey="name" width={140} tick={{ fontSize: 12 }} />
          <Tooltip formatter={(v) => usdFormatter.format(v as number)} />
          <Bar dataKey="totalSpend" className="fill-primary" />
          <ChartTooltip content={<ChartTooltipContent />} />
        </BarChart>
      </ChartContainer>
    </div>
  );
};

export default ModelSpends;
