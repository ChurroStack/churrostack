import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import type { LlmUsageSummaryItem } from '@/hooks/data/llms';
import { AlertCircle } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { usdFormatter } from './format-usd';

const intFormatter = new Intl.NumberFormat('en-US');

const LlmKpiCards = ({
  usage,
  error
}: {
  usage: LlmUsageSummaryItem[] | undefined;
  error?: string;
}) => {
  const { t } = useTranslation();

  const totals = useMemo(() => {
    const rows = usage ?? [];
    let requests = 0;
    let promptTokens = 0;
    let completionTokens = 0;
    let totalSpend = 0;
    for (const r of rows) {
      requests += r.completions ?? 0;
      promptTokens += r.promptTokens ?? 0;
      completionTokens += r.completionTokens ?? 0;
      totalSpend += r.totalSpend ?? 0;
    }
    const avgCost = requests > 0 ? totalSpend / requests : 0;
    return { requests, promptTokens, completionTokens, totalSpend, avgCost };
  }, [usage]);

  const titles = [t('Total Requests'), t('Total Tokens'), t('Avg Cost / Request'), t('Total Spent')];

  if (error) {
    return (
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {titles.map((title) => (
          <Card key={title}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-2 text-destructive text-sm">
                <AlertCircle className="size-4" />
                {t('Unavailable')}
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (usage === undefined) {
    return (
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {titles.map((title) => (
          <Card key={title}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
            </CardHeader>
            <CardContent>
              <Skeleton className="h-8 w-24" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">{t('Total Requests')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-semibold">{intFormatter.format(totals.requests)}</div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">{t('Total Tokens')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-semibold">
            {intFormatter.format(totals.promptTokens + totals.completionTokens)}
          </div>
          <div className="text-xs text-muted-foreground mt-1">
            {t('In')}: {intFormatter.format(totals.promptTokens)} · {t('Out')}:{' '}
            {intFormatter.format(totals.completionTokens)}
          </div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">{t('Avg Cost / Request')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-semibold">{usdFormatter.format(totals.avgCost)}</div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-muted-foreground">{t('Total Spent')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-semibold">{usdFormatter.format(totals.totalSpend)}</div>
        </CardContent>
      </Card>
    </div>
  );
};

export default LlmKpiCards;
