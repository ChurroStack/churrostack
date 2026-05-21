import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { useApplicationService } from '@/services/application-services';
import { Plus, ServerCog } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NewApplicationDialog } from './dialogs/new-application';

const Applications = () => {
  const { t } = useTranslation();
  const { data, isFetching, reload } = useApplicationService();
  const [showNewApplicationDialog, setShowNewApplicationDialog] = useState(false);

  const onCreateNewApplication = () => {
    setShowNewApplicationDialog(true);
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
          <EmptyTitle>{t('No applications yet')}</EmptyTitle>
          <EmptyDescription>
            {t("You haven't created any applications yet. Get started by creating your first application.")}
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <div className="flex gap-2">
            <Button onClick={onCreateNewApplication}>
              <Plus /> {t('Create new application')}
            </Button>
            <NewApplicationDialog
              showDialog={showNewApplicationDialog}
              onClose={() => setShowNewApplicationDialog(false)}
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
      <h1 className="text-2xl font-bold">Applications</h1>
      <div className="mt-4">{t('Select an application from left sidebar to view details.')}</div>
    </div>
  );
};

export default Applications;
