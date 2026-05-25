import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { ConfirmDialog } from '@/components/confirm-dialog';
import { useTranslation } from 'react-i18next';
import { EllipsisVertical, Gauge, Trash2 } from 'lucide-react';
import { useState } from 'react';

const EnvironmentContextMenu = ({
  name,
  canManage,
  onDeleteEnvironment,
  onAnalyzeUsage
}: {
  name: string;
  canManage?: boolean;
  onDeleteEnvironment?: (name: string) => void;
  onDeployEnvironment?: (name: string) => void;
  onAnalyzeUsage?: (name: string) => void;
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
        {canManage && onAnalyzeUsage && (
          <DropdownMenuItem
            onClick={() => {
              onAnalyzeUsage(name);
              setOpen(false);
            }}>
            <Gauge /> {t('Analyze application usage...')}
          </DropdownMenuItem>
        )}
        {canManage && onAnalyzeUsage && onDeleteEnvironment && <DropdownMenuSeparator />}
        {onDeleteEnvironment && (
          <ConfirmDialog
            title={t('Delete environment')}
            description={t('Are you sure you want to delete this environment?')}
            acceptText={t('Delete')}
            onAccept={async () => {
              onDeleteEnvironment(name);
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

export default EnvironmentContextMenu;
