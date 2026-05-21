import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { useApiKeyService } from '@/services/api-key-services';
import { Plus, ServerCog } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NewApiKeyDialog } from './dialogs/new-key';

const ApiKeys = () => {
  const { t } = useTranslation();
  const { data, isFetching, reload } = useApiKeyService();
  const [showNewApiKeyDialog, setShowNewApiKeyDialog] = useState(false);

  const onCreateNewApiKey = () => {
    setShowNewApiKeyDialog(true);
  };

  if (isFetching) {
    return <LoadingSkeleton maxCards={9} />;
  }
  if (!isFetching && data?.count === 0) {
    return (
      <Empty>
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <ServerCog />
          </EmptyMedia>
          <EmptyTitle>{t('No keys yet')}</EmptyTitle>
          <EmptyDescription>
            {t("You haven't created any keys yet. Get started by creating your first key.")}
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <div className="flex gap-2">
            <Button onClick={onCreateNewApiKey}>
              <Plus /> {t('Create new key')}
            </Button>

            <NewApiKeyDialog
              showDialog={showNewApiKeyDialog}
              onClose={() => setShowNewApiKeyDialog(false)}
              reload={reload}
            />
          </div>
        </EmptyContent>
        <Button variant="link" asChild className="text-muted-foreground" size="sm"></Button>
      </Empty>
    );
  }

  return (
    <div className="flex flex-col p-5">
      <h1 className="text-2xl font-bold">{t('ApiKeys')}</h1>
      <div className="mt-4">{t('Select an key from left sidebar to view details.')}</div>
    </div>
  );
};

export default ApiKeys;
