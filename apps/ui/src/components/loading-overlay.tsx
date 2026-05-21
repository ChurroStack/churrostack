'use client';
import { Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export function LoadingOverlay() {
  const { t } = useTranslation();
  return (
    <div className="absolute bg-gray-800/30 z-50 size-full rounded-lg animate-pulse">
      <div className="flex flex-1 items-center justify-center h-full gap-4 text-x">
        <Loader2 className="animate-spin" /> {t('Loading...')}
      </div>
    </div>
  );
}
