import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { useTranslation } from 'react-i18next';

export function ConfirmDialog({
  title,
  description,
  children,
  acceptText,
  cancelText,
  acceptVariant,
  onAccept,
  onCancel
}: {
  children: React.ReactNode;
  title: React.ReactNode | string;
  description: React.ReactNode | string;
  acceptText?: string;
  cancelText?: string;
  acceptVariant?: 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link';
  onAccept?: () => void;
  onCancel?: () => void;
}) {
  const { t } = useTranslation();

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>{children}</AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={onCancel}>{cancelText ?? t('Cancel')}</AlertDialogCancel>
          <AlertDialogAction asChild>
            <Button variant={acceptVariant ?? 'default'} onClick={onAccept}>
              {acceptText ?? t('Accept')}
            </Button>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
