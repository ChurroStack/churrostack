import { useDeleteApiKey, useGetKey } from '@/hooks/data/api-keys';
import { AlertCircle, ChartNoAxesCombined, KeyRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router';
import ApiKeyContextMenu from './menus/key-menu';
import { useApiKeyService } from '@/services/api-key-services';
import { Separator } from '@/components/ui/separator';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { LoadingSkeleton } from '@/components/loading-skeleton';
import { useNotifications } from '@/services/notification-service';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { formatDistanceToNow } from '@/extensions';
import { Badge } from '@/components/ui/badge';

const ApiKeyPage = () => {
  const { t } = useTranslation();
  const { id } = useParams();
  const { reload } = useApiKeyService();
  const { fetchAsync, data, isFetching, error } = useGetKey(id);
  const { error: deleteError, deleteAsync } = useDeleteApiKey();
  const navigate = useNavigate();
  const [apiKey, setApiKey] = useState(data);

  const { subscribe } = useNotifications();

  useEffect(() => {
    return subscribe((message) => {
      if (message.target === 'apiKey' && message.name === id) {
        fetchAsync('').then((result) => {
          setApiKey(result.data);
        });
      }
    });
  }, [id]);

  useEffect(() => {
    fetchAsync('').then((result) => {
      setApiKey(result.data);
    });
  }, [id]);

  const onDeleteApiKey = async (apiKeyId: string) => {
    await deleteAsync(apiKeyId);
    navigate('/keys');
    reload();
  };

  if (isFetching && !apiKey) {
    return <LoadingSkeleton maxCards={9} />;
  }

  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading api key')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between px-4 py-2 pb-0 max-w-full items-center">
        <h1 className="text-2xl font-bold flex flex-row items-center gap-2">
          <KeyRound /> <span>{apiKey?.identity.name}</span>
          {apiKey && apiKey.description && (
            <span className="text-sm font-normal mt-2 ml-2 truncate break-all max-w-200">{apiKey.description}</span>
          )}
        </h1>
        <div className="flex flex-row items-center gap-2">
          {apiKey && (
            <Badge variant="secondary">
              {t('Expires: ')} {formatDistanceToNow(apiKey?.expiresAt)}
            </Badge>
          )}
          <ApiKeyContextMenu onDeleteApiKey={onDeleteApiKey} id={apiKey?.id ?? ''} />
        </div>
      </div>
      <Separator className="my-2" />
      {deleteError && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error deleting application')}</AlertTitle>
          <AlertDescription>{deleteError}</AlertDescription>
        </Alert>
      )}
      <div className="flex flex-col gap-4 p-4 pt-0 min-h-0 w-full h-full">
        <Tabs defaultValue="events" className="flex flex-col min-h-0 w-full h-full">
          <TabsList>
            <TabsTrigger value="monitoring">
              <div className="flex flex-row items-center gap-2 px-2">
                <ChartNoAxesCombined /> {t('Monitoring')}
              </div>
            </TabsTrigger>
          </TabsList>
          <TabsContent value="monitoring" className="flex flex-col min-h-0 w-full h-full"></TabsContent>
        </Tabs>
      </div>
    </div>
  );
};

export default ApiKeyPage;
