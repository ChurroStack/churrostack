import InputWithCopy from '@/components/input-with-copy';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent } from '@/components/ui/card';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Progress } from '@/components/ui/progress';
import { useGetAccount, type QuotaItem } from '@/hooks/data/account';
import { AlertCircle } from 'lucide-react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { formatBytes } from '../../extensions';

const NetworkQuota = ({ quota }: { quota: QuotaItem }) => {
  const { t } = useTranslation();
  const percent = (quota.used / (quota.limit > 0 ? quota.limit : 1)) * 100;
  return (
    <Card className="flex flex-col gap-2 p-4 w-90 shadow-none">
      <CardContent className="px-2 flex flex-col gap-2">
        <div className="flex flex-row gap-2 items-center justify-between">
          <span className="text-sm font-semibold uppercase">{t('Data transferred')}</span>
          <div className="text-sm">
            {formatBytes(quota.used)} / {formatBytes(quota.limit)}
          </div>
        </div>
        <Progress value={percent} max={100} className="w-full" title={`${percent.toFixed(2)}%`} />
      </CardContent>
    </Card>
  );
};

const ApplicationQuota = ({ quota }: { quota: QuotaItem }) => {
  const { t } = useTranslation();
  const percent = (quota.used / (quota.limit > 0 ? quota.limit : 1)) * 100;
  return (
    <Card className="flex flex-col gap-2 p-4 w-90 shadow-none">
      <CardContent className="px-2 flex flex-col gap-2">
        <div className="flex flex-row gap-2 items-center justify-between">
          <span className="text-sm font-semibold uppercase">{t('Applications')}</span>
          <div className="text-sm">
            {quota.used} / {quota.limit}
          </div>
        </div>
        <Progress value={percent} max={100} className="w-full" title={`${percent.toFixed(2)}%`} />
      </CardContent>
    </Card>
  );
};

const EnvironmentQuota = ({ quota }: { quota: QuotaItem }) => {
  const { t } = useTranslation();
  const percent = (quota.used / (quota.limit > 0 ? quota.limit : 1)) * 100;
  return (
    <Card className="flex flex-col gap-2 p-4 w-90 shadow-none">
      <CardContent className="px-2 flex flex-col gap-2">
        <div className="flex flex-row gap-2 items-center justify-between">
          <span className="text-sm font-semibold uppercase">{t('Environments')}</span>
          <div className="text-sm">
            {quota.used} / {quota.limit}
          </div>
        </div>
        <Progress value={percent} max={100} className="w-full" title={`${percent.toFixed(2)}%`} />
      </CardContent>
    </Card>
  );
};

export default function GeneralPage() {
  const { t } = useTranslation();
  const { error, fetchAsync, data } = useGetAccount();

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  return (
    <div className="flex flex-1 flex-col gap-2 xl:space-y-4 bg-simple-card-glass px-4 py-2">
      {error && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error loading identities')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      <FieldGroup className="flex flex-col gap-2">
        <Field>
          <FieldLabel>{t('Name')}</FieldLabel>
          <InputWithCopy readOnly value={data?.name ?? 'name'} />
        </Field>
        <Field>
          <FieldLabel>{t('Owners')}</FieldLabel>
          <InputWithCopy readOnly value={data?.owners.join(', ') ?? ''} />
        </Field>
      </FieldGroup>
      <div className="flex flex-row gap-4">
        {data?.quotas.map((quota, idx) => {
          switch (quota.name) {
            case 'network':
              return <NetworkQuota key={idx} quota={quota} />;
            case 'applications':
              return <ApplicationQuota key={idx} quota={quota} />;
            case 'environments':
              return <EnvironmentQuota key={idx} quota={quota} />;
            default:
              return <></>;
          }
        })}
      </div>
    </div>
  );
}
