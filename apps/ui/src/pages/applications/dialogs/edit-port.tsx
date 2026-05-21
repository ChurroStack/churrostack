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
import { useUpdateApplication, type PortDefinition } from '@/hooks/data/applications';
import { AlertCircle } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

export default function EditPortDialog({
  appName,
  portName,
  ports,
  showDialog,
  onClose
}: {
  appName: string;
  portName: string;
  ports: PortDefinition[];
  showDialog: boolean;
  onClose: () => void;
}) {
  const { patchAsync, error, isFetching } = useUpdateApplication(appName);
  const { t } = useTranslation();
  const port = ports.find((p) => p.name === portName);
  const [portNumber, setPortNumber] = useState<string>(port?.port?.toString() ?? '8000');

  useEffect(() => {
    setPortNumber(port?.port?.toString() ?? '8000');
  }, [port?.port]);

  const onSave = async () => {
    const result = await patchAsync({
      ports: ports.map((p) => {
        if (p.name === portName) {
          return {
            ...p,
            port: parseInt(portNumber)
          };
        }
        return p;
      })
    });
    if (result.data && !result.error) {
      onClose();
    }
  };

  return (
    <AlertDialog open={showDialog}>
      <AlertDialogContent className="lg:min-w-150 min-w-full">
        <AlertDialogHeader className="mb-4">
          <AlertDialogTitle>{t('Edit port')}</AlertDialogTitle>
          <AlertDialogDescription>{t('Update the port number for the application')}</AlertDialogDescription>
        </AlertDialogHeader>
        {error && (
          <Alert className="mb-4" variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        {port && (
          <FieldSet className="w-full">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="port">{t('Port Number')}</FieldLabel>
                <Input
                  id="port"
                  type="number"
                  value={portNumber}
                  onChange={(e) => setPortNumber(e.target.value)}
                  required
                />
              </Field>
            </FieldGroup>
          </FieldSet>
        )}
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
