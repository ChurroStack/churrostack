import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { useEnvironmentService } from '@/services/environment-services';
import { Plus, ServerCog } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NewEnvironmentDialog } from './dialogs/new-environment';
import { useProfile } from '@/hooks/data/profile';

const Environments = () => {
  const { t } = useTranslation();
  const { data, isFetching, reload } = useEnvironmentService();
  const [showNewEnvironmentDialog, setShowNewEnvironmentDialog] = useState(false);
  const { profile } = useProfile();

  const onCreateNewEnvironment = () => {
    setShowNewEnvironmentDialog(true);
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
          <EmptyTitle>{t('No environments yet')}</EmptyTitle>
          <EmptyDescription>
            {t("You haven't created any environments yet. Get started by creating your first environment.")}
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <div className="flex gap-2">
            {profile?.role === 'administrator' && (
              <Button onClick={onCreateNewEnvironment}>
                <Plus /> {t('Create new environment')}
              </Button>
            )}
            <NewEnvironmentDialog
              showDialog={showNewEnvironmentDialog}
              onClose={() => setShowNewEnvironmentDialog(false)}
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
      <h1 className="text-2xl font-bold">{t('Environments')}</h1>
      <div className="mt-4">{t('Select an environment from left sidebar to view details.')}</div>
    </div>
  );
};

export default Environments;
