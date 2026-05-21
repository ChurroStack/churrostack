import { ConfirmDialog } from '@/components/confirm-dialog';
import InputWithCopy from '@/components/input-with-copy';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { randomString } from '@/extensions';
import { useUpdateApplication, type ApplicationEnvironmentVariable } from '@/hooks/data/applications';
import { AlertCircle, Plus, Save, Trash2, Undo } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';

const EditableRow = ({
  value,
  onChange,
  onDelete
}: {
  value: ApplicationEnvironmentVariable;
  onChange?: (key: string, value: string) => void;
  onDelete?: (key: string) => void;
}) => {
  const { t } = useTranslation();
  const [editable, setEditable] = useState(false);
  const [keyValue, setKeyValue] = useState(value?.name || '');
  const [valValue, setValValue] = useState(value?.value || '');

  return (
    <TableRow
      className="cursor-pointer"
      onClick={() => {
        if (!editable) {
          setEditable(true);
        }
      }}>
      <TableCell>
        {editable ? <InputWithCopy value={keyValue} onChange={(e) => setKeyValue(e)} /> : value.name}
      </TableCell>
      <TableCell className="flex flex-1 flex-row gap-2 items-center">
        {editable ? (
          <InputWithCopy className="flex-1" value={valValue} onChange={(e) => setValValue(e)} />
        ) : (
          <div className="flex flex-1">*****</div>
        )}
        {editable && (
          <Button
            size="sm"
            variant="default"
            onClick={() => {
              onChange?.(keyValue, valValue);
              setEditable(false);
            }}>
            <Save /> {t('Save')}{' '}
          </Button>
        )}
        {editable && (
          <Button
            size="sm"
            variant="secondary"
            onClick={() => {
              setEditable(false);
              setKeyValue(value.name);
              setValValue(value.value);
            }}>
            <Undo /> {t('Cancel')}{' '}
          </Button>
        )}
        <ConfirmDialog
          title={t('Delete environment variable')}
          description={t('Are you sure you want to delete this environment variable?')}
          acceptText={t('Delete')}
          onAccept={async () => {
            onDelete?.(value.name);
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
      </TableCell>
    </TableRow>
  );
};

const EnvironmentVariablesPanel = ({
  appName,
  appVariables
}: {
  appName: string;
  appVariables?: ApplicationEnvironmentVariable[];
  onEnvironmentVariableSet?: (name: string, value: string) => void;
}) => {
  const { t } = useTranslation();
  const { patchAsync, error, isFetching } = useUpdateApplication(appName);

  const [variables, setVariables] = useState(appVariables ?? []);

  useEffect(() => {
    setVariables(appVariables ?? []);
  }, [appVariables]);

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          <h3 className="text-sm text-muted-foreground">
            {t('Environment Variables for')} {appName}
          </h3>
        </div>
        <div className="flex flex-row items-center">
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              patchAsync({ variables }).then((response) => {
                if (!response.error) {
                  toast.success('Environment variables have been updated successfully.');
                }
              });
            }}>
            {isFetching ? <Spinner /> : <Save />} {t('Save Changes')}
          </Button>
        </div>
      </div>
      {error && (
        <div className="p-2">
          <Alert variant="destructive">
            <AlertCircle className="size-4" />
            <AlertTitle>{t('Error updating environment variables')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        </div>
      )}
      <div className="flex flex-col h-full overflow-auto">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('Name')}</TableHead>
              <TableHead>{t('Value')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {variables.map((env) => (
              <EditableRow
                key={`row-${env.name}`}
                value={env}
                onChange={(newKey, newValue) => {
                  setVariables([...variables.filter((v) => v.name !== env.name), { name: newKey, value: newValue }]);
                }}
                onDelete={(delKey) => {
                  setVariables(variables.filter((v) => v.name !== delKey));
                }}
              />
            ))}
          </TableBody>
        </Table>
        <div className="flex flex-row justify-center">
          <Button
            className="mt-4"
            variant="link"
            size="sm"
            onClick={() => {
              setVariables([...variables, { name: `NEW_VARIABLE_${randomString()}`, value: 'value' }]);
            }}>
            <Plus /> {t('Add Environment Variable')}
          </Button>
        </div>
      </div>
    </div>
  );
};

export default EnvironmentVariablesPanel;
