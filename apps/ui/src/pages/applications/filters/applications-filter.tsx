import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import EnvironmentPicker from '@/pickers/environment-picker';
import IdentityPicker from '@/pickers/identity-picker';
import { Filter } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface ApplicationsFilterProps {
  environment?: string;
  createdBy?: string;
  onEnvironmentChange: (value?: string) => void;
  onCreatedByChange: (value?: string) => void;
}

export function ApplicationsFilter({
  environment,
  createdBy,
  onEnvironmentChange,
  onCreatedByChange
}: ApplicationsFilterProps) {
  const { t } = useTranslation();
  const hasActiveFilter = !!environment || !!createdBy;

  const clearAll = () => {
    onEnvironmentChange(undefined);
    onCreatedByChange(undefined);
  };

  return (
    <Popover>
      <Tooltip>
        <TooltipTrigger asChild>
          <PopoverTrigger asChild>
            <Button size="icon" className="size-6 relative" variant="secondary" aria-label={t('Filter')}>
              <Filter />
              {hasActiveFilter && (
                <span className="absolute -top-0.5 -right-0.5 size-2 rounded-full bg-primary" />
              )}
            </Button>
          </PopoverTrigger>
        </TooltipTrigger>
        <TooltipContent>
          <p>{t('Filter applications')}</p>
        </TooltipContent>
      </Tooltip>
      <PopoverContent align="end" className="w-72 p-3">
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-muted-foreground">{t('Environment')}</label>
            <EnvironmentPicker
              className="w-full"
              value={environment ?? ''}
              onChange={(v) => onEnvironmentChange(v || undefined)}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-muted-foreground">{t('Created by')}</label>
            <IdentityPicker
              className="w-full"
              type="user"
              value={createdBy ?? ''}
              onChange={(v) => onCreatedByChange(v || undefined)}
            />
          </div>
          {hasActiveFilter && (
            <div className="flex justify-end pt-1 border-t">
              <Button variant="ghost" size="sm" onClick={clearAll}>
                {t('Clear all')}
              </Button>
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

export default ApplicationsFilter;
