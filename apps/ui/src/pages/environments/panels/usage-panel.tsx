import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatBytes, formatDistanceToNow } from '@/extensions';
import {
  useAnalyzeEnvironmentUsage,
  useGetEnvironmentUsage,
  type EnvironmentUsageItem
} from '@/hooks/data/environments';
import { AlertCircle, ArrowDown, ArrowUp, Gauge } from 'lucide-react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

function RecommendationCell({ usage }: { usage: EnvironmentUsageItem }) {
  const { t } = useTranslation();

  if (usage.direction === 'not_analyzed') {
    return <span className="text-muted-foreground text-xs">{t('Not analyzed')}</span>;
  }
  if (usage.direction === 'insufficient_data') {
    return <span className="text-muted-foreground text-xs">{t('Not enough data')}</span>;
  }
  if (!usage.hasRecommendation || !usage.recommendedSize) {
    return (
      <Badge variant="outline" className="text-green-700 border-green-600">
        {t('Optimal')}
      </Badge>
    );
  }

  const Icon = usage.direction === 'upsize' ? ArrowUp : ArrowDown;
  return (
    <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-600">
      <Icon className="size-3" /> {usage.recommendedSize.hint}
    </Badge>
  );
}

const EnvironmentUsagePanel = ({
  environmentName,
  reloadSignal
}: {
  environmentName: string;
  reloadSignal?: number;
}) => {
  const { t } = useTranslation();
  const { data: usage, fetchAsync, isFetching, error } = useGetEnvironmentUsage(environmentName);
  const { postAsync: analyzeAllAsync, isFetching: isAnalyzing } = useAnalyzeEnvironmentUsage(environmentName);

  useEffect(() => {
    fetchAsync('');
  }, [environmentName, fetchAsync, reloadSignal]);

  const onAnalyzeAll = async () => {
    const result = await analyzeAllAsync();
    if (result.error) {
      toast.error(result.error);
      return;
    }
    toast.success(t('Usage analysis completed.'));
    await fetchAsync('');
  };

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 items-center">
        <h3 className="text-sm text-muted-foreground">
          {t('Historic CPU and memory usage and recommended size per application')}
        </h3>
        <Button variant="default" size="sm" disabled={isAnalyzing} onClick={onAnalyzeAll}>
          {isAnalyzing ? <Spinner /> : <Gauge />} {t('Analyze all')}
        </Button>
      </div>
      {error && (
        <div className="p-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error loading usage')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col h-full overflow-auto">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Application')}</TableHead>
              <TableHead>{t('Current size')}</TableHead>
              <TableHead>{t('CPU avg / max (cores)')}</TableHead>
              <TableHead>{t('Memory avg / max')}</TableHead>
              <TableHead>{t('Recommended size')}</TableHead>
              <TableHead>{t('Analyzed')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {usage?.map((row) => (
              <TableRow key={row.applicationName}>
                <TableCell className="font-medium">{row.applicationName}</TableCell>
                <TableCell className="font-mono text-xs">{row.currentSize?.hint ?? '-'}</TableCell>
                <TableCell className="font-mono text-xs">
                  {row.computedAt ? `${row.cpuAvg.toFixed(2)} / ${row.cpuMax.toFixed(2)}` : '-'}
                </TableCell>
                <TableCell className="font-mono text-xs">
                  {row.computedAt ? `${formatBytes(row.memoryAvg)} / ${formatBytes(row.memoryMax)}` : '-'}
                </TableCell>
                <TableCell>
                  <RecommendationCell usage={row} />
                </TableCell>
                <TableCell className="text-xs text-muted-foreground">
                  {row.computedAt ? formatDistanceToNow(row.computedAt) : '-'}
                </TableCell>
              </TableRow>
            ))}
            {usage && usage.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-center text-muted-foreground py-6">
                  {t('No applications in this environment.')}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
        {isFetching && !usage && (
          <div className="flex justify-center p-6">
            <Spinner />
          </div>
        )}
      </div>
    </div>
  );
};

export default EnvironmentUsagePanel;
