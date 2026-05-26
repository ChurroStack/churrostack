import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ChartContainer, ChartTooltip, ChartTooltipContent } from '@/components/ui/chart';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, ChartColumn } from 'lucide-react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Bar, BarChart, CartesianGrid, Tooltip, XAxis, YAxis } from 'recharts';
import { type ChartConfig } from '@/components/ui/chart';
import { format } from 'date-fns/format';
import { formatDateTime, getBrowserTz } from '@/extensions';
import { useGetLlmMetric } from '@/hooks/data/llms';

const chartConfig = {
  value: {
    label: 'Desktop',
    color: '#2563eb'
  }
} satisfies ChartConfig;

const TokensUsageChart = ({
  llmId,
  metricName,
  title,
  maxValue,
  fromDate,
  toDate,
  identityName,
  userId,
  model
}: {
  llmId?: string;
  metricName: string;
  title: string;
  maxValue?: number;
  fromDate: Date | undefined;
  toDate: Date | undefined;
  identityName?: string;
  userId?: string;
  model?: string;
}) => {
  const { t } = useTranslation();
  const { fetchAsync, isFetching, error, data } = useGetLlmMetric(llmId, metricName);

  useEffect(() => {
    const params = new URLSearchParams();
    if (fromDate) params.set('from', fromDate.toISOString());
    if (toDate) params.set('to', toDate.toISOString());
    params.set('tz', getBrowserTz());
    if (identityName) params.set('identityName', identityName);
    if (userId) params.set('userId', userId);
    if (model) params.set('model', model);
    fetchAsync(params.toString());
  }, [llmId, fromDate, toDate, identityName, userId, model]);
  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading chart data')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  if (isFetching || !data) {
    return (
      <div className="flex items-center space-x-4">
        <Skeleton className="h-12 w-12 rounded-full" />
        <div className="space-y-2">
          <Skeleton className="h-4 w-[250px]" />
          <Skeleton className="h-4 w-[200px]" />
        </div>
      </div>
    );
  }

  if (!data.values?.length) {
    return (
      <div className="overflow-hidden rounded-md border flex flex-col gap-4 min-h-0 w-full h-full p-2">
        <h3 className="text-sm font-semibold">{title ?? t('Token Usage')}</h3>
        <div className="flex flex-1 flex-col items-center justify-center gap-3 py-10 text-muted-foreground">
          <ChartColumn className="size-10 opacity-50" />
          <p className="text-sm">{t('No data captured yet for this metric')}</p>
        </div>
      </div>
    );
  }
  return (
    <div className="overflow-hidden rounded-md border flex flex-col gap-4 min-h-0 w-full h-full p-2">
      <h3 className="text-sm font-semibold">{title ?? t('Token Usage')}</h3>
      <ChartContainer config={chartConfig} title={title ?? t('Token Usage')} className="h-[200px] w-full">
        <BarChart accessibilityLayer data={data?.values ?? []}>
          <CartesianGrid vertical={false} />
          <XAxis
            dataKey="timestamp"
            domain={['dataMin', 'dataMax']}
            axisLine={false}
            tickFormatter={(value) => {
              if (value === undefined || value === null || value === 'dataMin' || value === 'dataMax') {
                return '';
              }
              return format(new Date(value), 'HH:mm');
            }}
            tick={{ fontSize: 12 }}
            interval={5}
          />
          <YAxis
            width={80}
            tickCount={25}
            axisLine={false}
            domain={[0, maxValue ?? 'auto']}
            tickFormatter={(value) => value}
          />
          <Tooltip
            labelFormatter={(value) => {
              if (value === undefined || value === null || value === 'dataMin' || value === 'dataMax') {
                return '';
              }
              return formatDateTime(new Date(value as number), 'yyyy-MM-dd HH:mm');
            }}
            formatter={(value) => value}
          />
          <Bar dataKey="value" className="fill-primary" />
          <ChartTooltip content={<ChartTooltipContent />} />
        </BarChart>
      </ChartContainer>
    </div>
  );
};

export default TokensUsageChart;
