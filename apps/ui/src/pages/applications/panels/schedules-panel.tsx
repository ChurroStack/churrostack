import { ConfirmDialog } from '@/components/confirm-dialog';
import { Button } from '@/components/ui/button';
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldGroup, FieldLabel, FieldSet } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { AlertCircle, Plus, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import UpsertApplicationSchedule from '../dialogs/upsert-application-schedule';
import {
  useDeleteApplicationSchedules,
  useGetApplicationSchedules,
  useUpsertApplicationSchedule
} from '@/hooks/data/application-schedules';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

export const SchedulesPanel = ({ appName }: { appName: string }) => {
  const { t } = useTranslation();
  const [showNew, setShowNew] = useState(false);
  const { data: schedules, fetchAsync, error } = useGetApplicationSchedules(appName);
  const { deleteAsync, error: deleteError } = useDeleteApplicationSchedules(appName);
  const { postAsync } = useUpsertApplicationSchedule(appName);

  const reload = async () => {
    await fetchAsync('');
  };

  useEffect(() => {
    reload();
  }, [appName]);

  return (
    <div className="rounded-md border flex flex-col min-h-0 w-full h-full overflow-auto">
      {showNew && (
        <UpsertApplicationSchedule
          appName={appName}
          showDialog={showNew}
          onClose={() => {
            setShowNew(false);
            reload();
          }}
        />
      )}
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">{t('Manage application schedules')}</h3>
        </div>
        <div className="flex flex-row items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              setShowNew(true);
            }}>
            <Plus />
            {t('Add Schedule')}
          </Button>
        </div>
      </div>
      <div className="flex flex-col p-2 gap-4">
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {deleteError && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error deleting schedule')}</AlertTitle>
            <AlertDescription>{deleteError}</AlertDescription>
          </Alert>
        )}
        {schedules?.items?.map((schedule) => (
          <Card key={schedule.name}>
            <CardHeader>
              <CardTitle className="flex gap-2 items-center">
                <Switch
                  checked={schedule.enabled}
                  onCheckedChange={async (checked) => {
                    const newSchedule = { ...schedule, enabled: checked };
                    const result = await postAsync(newSchedule);
                    if (!result.error) {
                      reload();
                    }
                  }}
                />
                <span>{schedule.name || t('New Schedule')}</span>
              </CardTitle>
              <CardDescription>{schedule.description}</CardDescription>
              <CardAction>
                <ConfirmDialog
                  title={t('Delete schedule')}
                  description={t('Are you sure you want to delete this schedule?')}
                  acceptText={t('Delete')}
                  onAccept={async () => {
                    const result = await deleteAsync(schedule.name);
                    if (!result.error) {
                      reload();
                    }
                  }}
                  onCancel={() => {}}>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={(e) => {
                      e.stopPropagation();
                    }}>
                    <Trash2 />
                  </Button>
                </ConfirmDialog>
              </CardAction>
            </CardHeader>
            <CardContent>
              <FieldSet className="w-full">
                <FieldGroup className="flex flex-row">
                  <Field className="max-w-40">
                    <FieldLabel htmlFor="cron">{t('Cron Expression')}</FieldLabel>
                    <Input id="cron" type="text" value={schedule.cronExpression} readOnly />
                  </Field>
                  <Field className="max-w-40">
                    <FieldLabel htmlFor="method">{t('Method')}</FieldLabel>
                    <Input id="method" type="text" value={schedule.httpRequest.method} readOnly />
                  </Field>
                  <Field>
                    <FieldLabel htmlFor="path">{t('Path')}</FieldLabel>
                    <Input id="path" type="text" value={schedule.httpRequest.path} readOnly />
                  </Field>
                </FieldGroup>
              </FieldSet>
              {schedule?.httpRequest?.body && (
                <FieldSet className="w-full">
                  <FieldGroup className="flex flex-row">
                    <Field>
                      <FieldLabel htmlFor="body">{t('Body')}</FieldLabel>
                      <Textarea id="body" value={schedule.httpRequest.body} readOnly />
                    </Field>
                  </FieldGroup>
                </FieldSet>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
};
