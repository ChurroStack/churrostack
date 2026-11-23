import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Field, FieldDescription, FieldGroup, FieldLabel, FieldSet } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Spinner } from '@/components/ui/spinner';
import { Switch } from '@/components/ui/switch';
import { formatDateTime } from '@/extensions';
import {
  useEntraSyncConfig,
  useSaveEntraSyncConfig,
  useStartEntraSync,
  type EntraSyncConfig,
  type EntraSyncStatus
} from '@/hooks/data/identity-sync';
import { AlertCircle, Plus, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

/** Properties most tenants filter on; any other Graph user property can still be typed. */
const suggestedProperties = [
  'companyName',
  'department',
  'jobTitle',
  'officeLocation',
  'country',
  'city',
  'employeeType',
  'usageLocation'
];

interface RuleDraft {
  property: string;
  /** Comma separated, e.g. "A, B, C" (OR). */
  values: string;
}

const defaultCron = '0 * * * *';

const parseValues = (value: string) =>
  value
    .split(',')
    .map((v) => v.trim())
    .filter((v) => v.length > 0);

export function EntraSyncDialog({
  showDialog,
  lastStatus,
  onClose,
  onSaved
}: {
  showDialog: boolean;
  lastStatus?: EntraSyncStatus;
  onClose: () => void;
  /** Called after the settings were saved (and, when requested, a run was queued). */
  onSaved: (started: boolean) => void;
}) {
  const { t } = useTranslation();
  const { isFetching: isLoading, error: loadError, data: loaded, fetchAsync: loadConfig } = useEntraSyncConfig();
  const { isFetching: isSaving, error: saveError, putAsync } = useSaveEntraSyncConfig();
  const { isFetching: isStarting, error: startError, startAsync } = useStartEntraSync();

  const [enabled, setEnabled] = useState(false);
  const [tenantId, setTenantId] = useState('');
  const [cron, setCron] = useState(defaultCron);
  const [filterEnabled, setFilterEnabled] = useState(false);
  const [rules, setRules] = useState<RuleDraft[]>([]);

  useEffect(() => {
    if (showDialog) loadConfig('');
  }, [showDialog]);

  useEffect(() => {
    if (!loaded) return;
    setEnabled(loaded.config.enabled);
    setTenantId(loaded.config.tenantId ?? '');
    setCron(loaded.config.cronExpression || defaultCron);
    setFilterEnabled(loaded.config.filter.enabled);
    setRules(loaded.config.filter.rules.map((r) => ({ property: r.property, values: r.values.join(', ') })));
  }, [loaded]);

  const available = loaded?.available ?? true;
  const busy = isSaving || isStarting;
  const error = loadError || saveError || startError;

  const buildConfig = (): EntraSyncConfig => ({
    enabled,
    cronExpression: cron.trim() || defaultCron,
    filter: {
      enabled: filterEnabled,
      rules: rules
        .map((r) => ({ property: r.property.trim(), values: parseValues(r.values) }))
        .filter((r) => r.property.length > 0 || r.values.length > 0)
    }
  });

  const onSave = async (syncNow: boolean) => {
    const saved = await putAsync(buildConfig());
    if (!saved.data || saved.error) return;
    if (syncNow) {
      const started = await startAsync();
      if (!started.data || started.error) return;
    }
    onSaved(syncNow);
  };

  const updateRule = (index: number, patch: Partial<RuleDraft>) =>
    setRules((prev) => prev.map((r, i) => (i === index ? { ...r, ...patch } : r)));

  return (
    <AlertDialog open={showDialog}>
      <AlertDialogContent className="lg:min-w-175 min-w-full max-h-[90vh] overflow-y-auto">
        <AlertDialogHeader className="mb-2">
          <AlertDialogTitle>{t('Sync from Microsoft Entra')}</AlertDialogTitle>
          <AlertDialogDescription>
            {t(
              'Keep users and groups in sync with your Microsoft Entra tenant. Synced users get the User role and join the "users" group.'
            )}
          </AlertDialogDescription>
        </AlertDialogHeader>
        {error && (
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {loaded && !loaded.available && (
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Microsoft app registration not configured')}</AlertTitle>
            <AlertDescription>
              {t(
                'Set ExternalProviders:Microsoft:ClientId, ClientSecret and TenantId on the server (application permissions User.Read.All and GroupMember.Read.All with admin consent).'
              )}
            </AlertDescription>
          </Alert>
        )}
        {isLoading && !loaded ? (
          <div className="flex flex-col gap-3">
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : (
          <>
            <FieldSet className="w-full" disabled={!available}>
              <FieldGroup className="flex flex-row gap-4">
                <Field className="flex-1">
                  <FieldLabel htmlFor="entra-tenant">{t('Tenant ID')}</FieldLabel>
                  <Input
                    id="entra-tenant"
                    placeholder="contoso.onmicrosoft.com"
                    value={tenantId}
                    readOnly
                    disabled
                  />
                  <FieldDescription>{t('Set by the server (ExternalProviders:Microsoft:TenantId); it cannot be changed here.')}</FieldDescription>
                </Field>
              </FieldGroup>
              <FieldGroup className="flex flex-row items-end gap-4">
                <Field orientation="horizontal" className="w-auto items-center pb-2">
                  <Switch id="entra-schedule-enabled" checked={enabled} onCheckedChange={setEnabled} />
                  <FieldLabel htmlFor="entra-schedule-enabled">{t('Scheduled sync')}</FieldLabel>
                </Field>
                <Field className="max-w-60">
                  <FieldLabel htmlFor="entra-cron">{t('Cron expression (UTC)')}</FieldLabel>
                  <Input id="entra-cron" value={cron} disabled={!enabled} onChange={(e) => setCron(e.target.value)} />
                </Field>
                <FieldDescription className="flex-1 pb-2">{t('Default: every hour (0 * * * *).')}</FieldDescription>
              </FieldGroup>
              <FieldGroup>
                <Field>
                  <Field orientation="horizontal" className="items-center">
                    <Switch id="entra-filter-enabled" checked={filterEnabled} onCheckedChange={setFilterEnabled} />
                    <FieldLabel htmlFor="entra-filter-enabled">{t('Filter users')}</FieldLabel>
                  </Field>
                  <FieldDescription>
                    {t(
                      'Only matching users (and groups with at least one matching member) are synced. Users that stop matching are disabled.'
                    )}
                  </FieldDescription>
                </Field>
              </FieldGroup>
              {filterEnabled && (
                <div className="flex flex-col gap-2 rounded-md border p-3">
                  <datalist id="entra-properties">
                    {suggestedProperties.map((p) => (
                      <option key={p} value={p} />
                    ))}
                  </datalist>
                  {rules.map((rule, index) => (
                    <div key={index} className="flex flex-row items-center gap-2">
                      <Input
                        className="max-w-48"
                        list="entra-properties"
                        aria-label={t('Property')}
                        placeholder={t('Property (e.g. companyName)')}
                        value={rule.property}
                        onChange={(e) => updateRule(index, { property: e.target.value })}
                      />
                      <span className="text-sm text-muted-foreground">=</span>
                      <Input
                        aria-label={t('Values')}
                        placeholder={t('A, B, C')}
                        value={rule.values}
                        onChange={(e) => updateRule(index, { values: e.target.value })}
                      />
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={t('Remove condition')}
                        onClick={() => setRules((prev) => prev.filter((_, i) => i !== index))}>
                        <Trash2 />
                      </Button>
                    </div>
                  ))}
                  <div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setRules((prev) => [...prev, { property: '', values: '' }])}>
                      <Plus /> {t('Add condition')}
                    </Button>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {t('Values in a row match any of them (OR); all rows must match (AND).')}
                  </p>
                </div>
              )}
            </FieldSet>
            {lastStatus && lastStatus.state !== 'idle' && (
              <p className="text-xs text-muted-foreground">
                {t('Last run')}: {lastStatus.state}
                {lastStatus.finishedAt ? ` · ${formatDateTime(lastStatus.finishedAt)}` : ''}
                {lastStatus.summary ? ` · ${lastStatus.summary}` : ''}
              </p>
            )}
          </>
        )}
        <AlertDialogFooter className="mt-2">
          <Button variant="outline" disabled={busy} onClick={onClose}>
            {t('Cancel')}
          </Button>
          <Button variant="secondary" disabled={busy || !available || isLoading} onClick={() => onSave(false)}>
            {isSaving && !isStarting && <Spinner />} {t('Save')}
          </Button>
          <Button disabled={busy || !available || isLoading} onClick={() => onSave(true)}>
            {busy && <Spinner />} {t('Save & sync now')}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
