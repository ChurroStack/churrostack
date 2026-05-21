import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useTranslation } from 'react-i18next';
import { EllipsisVertical, Trash2 } from 'lucide-react';
import { useState } from 'react';

const ApiKeyContextMenu = ({ id, onDeleteApiKey }: { id: string; onDeleteApiKey?: (id: string) => void }) => {
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
        {onDeleteApiKey && <DropdownMenuSeparator />}
        {onDeleteApiKey && (
          <ConfirmDialog
            title={t('Delete API key')}
            description={t('Are you sure you want to delete this API key?')}
            acceptText={t('Delete')}
            onAccept={async () => {
              onDeleteApiKey(id);
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

export default ApiKeyContextMenu;
