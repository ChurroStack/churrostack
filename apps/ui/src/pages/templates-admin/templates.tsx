import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Button } from '@/components/ui/button';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { useTemplateService } from '@/services/template-services';
import { Plus, ServerCog } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NewTemplateDialog } from './dialogs/new-template';

const Templates = () => {
  const { t } = useTranslation();
  const { data, isFetching, reload } = useTemplateService();
  const [showNewTemplateDialog, setShowNewTemplateDialog] = useState(false);

  const onCreateNewTemplate = () => {
    setShowNewTemplateDialog(true);
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
          <EmptyTitle>{t('No templates yet')}</EmptyTitle>
          <EmptyDescription>
            {t("You haven't created any templates yet. Get started by creating your first template.")}
          </EmptyDescription>
        </EmptyHeader>
        <EmptyContent>
          <div className="flex gap-2">
            <Button onClick={onCreateNewTemplate}>
              <Plus /> {t('Create new template')}
            </Button>

            <NewTemplateDialog
              showDialog={showNewTemplateDialog}
              onClose={() => setShowNewTemplateDialog(false)}
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
      <h1 className="text-2xl font-bold">Templates</h1>
      <div className="mt-4">{t('Select an template from left sidebar to view details.')}</div>
    </div>
  );
};

export default Templates;
