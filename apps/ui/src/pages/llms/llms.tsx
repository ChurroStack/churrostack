import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { useLlmService } from '@/services/llm-services';
import { Brain, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NewLlmDialog } from './dialogs/new-llm';
import { useProfile } from '@/hooks/data/profile';

const Llms = () => {
  const { t } = useTranslation();
  const { data, isFetching, reload } = useLlmService();
  const [showNewLlmDialog, setShowNewLlmDialog] = useState(false);
  const { profile } = useProfile();

  const onCreateNewLlm = () => {
    setShowNewLlmDialog(true);
  };

  if (isFetching) {
    return <LoadingSkeleton maxCards={9} />;
  }
  if (!isFetching && data?.count === 0) {
    return (
      <Empty>
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <Brain />
          </EmptyMedia>
          <EmptyTitle>{t('No llms yet')}</EmptyTitle>
          <EmptyDescription>
            {t("You haven't created any llms yet. Get started by creating your first llm.")}
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <div className="flex gap-2">
            {profile?.role === 'administrator' && (
              <Button onClick={onCreateNewLlm}>
                <Plus /> {t('Create new llm')}
              </Button>
            )}
            <NewLlmDialog showDialog={showNewLlmDialog} onClose={() => setShowNewLlmDialog(false)} reload={reload} />
          </div>
        </EmptyContent>
        <Button variant="link" asChild className="text-muted-foreground" size="sm"></Button>
      </Empty>
    );
  }

  return (
    <div className="flex flex-col p-5">
      <h1 className="text-2xl font-bold">{t('Llms')}</h1>
      <div className="mt-4">{t('Select an llm from left sidebar to view details.')}</div>
    </div>
  );
};

export default Llms;
