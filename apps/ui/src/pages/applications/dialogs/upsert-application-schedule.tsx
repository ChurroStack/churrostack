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
import { Field, FieldGroup, FieldLabel, FieldSet } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';
import { Textarea } from '@/components/ui/textarea';
import { useUpsertApplicationSchedule, type ApplicationScheduleItem } from '@/hooks/data/application-schedules';
import { AlertCircle } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

const defaultSchedule = {
  name: '',
  enabled: true,
  cronExpression: '0 0 * * *',
  httpRequest: {
    method: 'GET',
    path: '/',
    headers: [],
    body: undefined
  }
};

export default function UpsertApplicationSchedule({
  appName,
  schedule: originalSchedule,
  showDialog,
  onClose
}: {
  appName: string;
  schedule?: ApplicationScheduleItem;
  showDialog: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const { isFetching, error, postAsync } = useUpsertApplicationSchedule(appName);
  const [schedule, setSchedule] = useState<ApplicationScheduleItem>(originalSchedule ?? defaultSchedule);

  useEffect(() => {
    setSchedule(originalSchedule ?? defaultSchedule);
  }, [originalSchedule]);

  const onSave = async () => {
    const result = await postAsync(schedule);
    if (result.data && !result.error) {
      onClose();
    }
  };

  return (
    <AlertDialog open={showDialog}>
      <AlertDialogContent className="lg:min-w-150 min-w-full">
        <AlertDialogHeader className="mb-4">
          <AlertDialogTitle>{originalSchedule ? t('Edit schedule') : t('New schedule')}</AlertDialogTitle>
          <AlertDialogDescription>{t('Update the schedule for the application')}</AlertDialogDescription>
        </AlertDialogHeader>
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        <FieldSet className="w-full">
          <FieldGroup className="flex flex-row gap-2">
            <Field className="max-w-40">
              <FieldLabel htmlFor="name">{t('Name')}</FieldLabel>
              <Input
                id="name"
                value={schedule.name}
                onChange={(e) => {
                  setSchedule((prev) => {
                    return { ...prev, name: e.target.value };
                  });
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="description">{t('Brief description')}</FieldLabel>
              <Input
                id="description"
                value={schedule.description}
                onChange={(e) => {
                  setSchedule((prev) => {
                    return { ...prev, description: e.target.value };
                  });
                }}
              />
            </Field>
          </FieldGroup>
        </FieldSet>
        <FieldSet className="w-full">
          <FieldGroup className="flex flex-row gap-2">
            <Field className="max-w-40">
              <FieldLabel htmlFor="cron">{t('Cron Expression')}</FieldLabel>
              <Input
                id="cron"
                type="text"
                value={schedule.cronExpression}
                onChange={(e) => {
                  setSchedule((prev) => {
                    return { ...prev, cronExpression: e.target.value };
                  });
                }}
              />
            </Field>
            <Field className="max-w-20">
              <FieldLabel htmlFor="method">{t('Method')}</FieldLabel>
              <Input
                id="method"
                type="text"
                value={schedule.httpRequest.method}
                onChange={(e) => {
                  setSchedule((prev) => {
                    return { ...prev, httpRequest: { ...prev.httpRequest, method: e.target.value } };
                  });
                }}
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="path">{t('Path')}</FieldLabel>
              <Input
                id="path"
                type="text"
                value={schedule.httpRequest.path}
                onChange={(e) => {
                  setSchedule((prev) => {
                    return { ...prev, httpRequest: { ...prev.httpRequest, path: e.target.value } };
                  });
                }}
              />
            </Field>
          </FieldGroup>
        </FieldSet>
        <FieldSet className="w-full">
          <FieldGroup className="flex flex-row">
            <Field>
              <FieldLabel htmlFor="body">{t('Body')}</FieldLabel>
              <Textarea
                id="body"
                value={schedule.httpRequest.body}
                onChange={(e) => {
                  setSchedule((prev) => {
                    return { ...prev, httpRequest: { ...prev.httpRequest, body: e.target.value } };
                  });
                }}
              />
            </Field>
          </FieldGroup>
        </FieldSet>
        <AlertDialogFooter>
          <Button
            variant="secondary"
            disabled={isFetching}
            onClick={(e) => {
              onClose();
              e.stopPropagation();
              e.preventDefault();
            }}>
            {t('Cancel')}
          </Button>
          <Button
            disabled={isFetching}
            onClick={(e) => {
              onSave();
              e.stopPropagation();
              e.preventDefault();
            }}>
            {isFetching && <Spinner className="text-white-600 size-5" />} {t('Save')}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
