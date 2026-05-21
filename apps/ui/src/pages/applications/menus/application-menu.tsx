import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useTranslation } from 'react-i18next';
import { CloudUpload, EllipsisVertical, Trash2 } from 'lucide-react';
import { useState } from 'react';

const ApplicationContextMenu = ({
  name,
  onDeleteApplication,
  onDeployApplication
}: {
  name: string;
  onDeleteApplication?: (name: string) => void;
  onDeployApplication?: (name: string) => void;
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <div className="width-10">
          <EllipsisVertical size="14" />
        </div>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-56" align="start">
        {onDeployApplication && (
          <ConfirmDialog
            title={t('Deploy application')}
            description={t('Are you sure you want to deploy this application?')}
            acceptText={t('Deploy')}
            onAccept={async () => {
              onDeployApplication(name);
              setOpen(false);
            }}
            onCancel={() => setOpen(false)}>
            <DropdownMenuItem onSelect={(e) => e.preventDefault()}>
              <CloudUpload /> {t('Deploy')}
            </DropdownMenuItem>
          </ConfirmDialog>
        )}
        {onDeleteApplication && <DropdownMenuSeparator />}
        {onDeleteApplication && (
          <ConfirmDialog
            title={t('Delete application')}
            description={t('Are you sure you want to delete this application?')}
            acceptText={t('Delete')}
            onAccept={async () => {
              onDeleteApplication(name);
              setOpen(false);
            }}
            onCancel={() => setOpen(false)}>
            <DropdownMenuItem className="text-destructive" onSelect={(e) => e.preventDefault()}>
              <Trash2 className="text-destructive" /> {t('Delete')}
            </DropdownMenuItem>
          </ConfirmDialog>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
};

export default ApplicationContextMenu;
