import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import EnvironmentPicker from '@/pickers/environment-picker';
import IdentityPicker from '@/pickers/identity-picker';
import TagFilterPicker from '@/pickers/tag-picker';
import type { TagPermission } from '@/hooks/data/tags';
import { Filter } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface ApplicationsFilterFieldsProps {
  environment?: string;
  createdBy?: string;
  tags?: string[];
  permission?: TagPermission;
  hideEnvironment?: boolean;
  onEnvironmentChange?: (value?: string) => void;
  onCreatedByChange?: (value?: string) => void;
  onTagsChange?: (value: string[]) => void;
}

/**
 * Pure field block (Environment / Created by / Tags / Clear all) intended to be slotted
 * inside any popover. Used both by `ApplicationsFilter` (the sidebar's icon-button + popover)
 * and by `SearchAndFilter`'s `filterContent` on the Home page and Env→Applications tab.
 */
export function ApplicationsFilterContent({
  environment,
  createdBy,
  tags = [],
  permission = 'read',
  hideEnvironment,
  onEnvironmentChange,
  onCreatedByChange,
  onTagsChange
}: ApplicationsFilterFieldsProps) {
  const { t } = useTranslation();
  const hasActiveFilter = !!environment || !!createdBy || tags.length > 0;

  const clearAll = () => {
    onEnvironmentChange?.(undefined);
    onCreatedByChange?.(undefined);
    onTagsChange?.([]);
  };

  return (
    <div className="flex flex-col gap-3">
      {!hideEnvironment && onEnvironmentChange && (
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-medium text-muted-foreground">{t('Environment')}</label>
          <EnvironmentPicker
            className="w-full"
            value={environment ?? ''}
            onChange={(v) => onEnvironmentChange(v || undefined)}
          />
        </div>
      )}
      {onCreatedByChange && (
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-medium text-muted-foreground">{t('Created by')}</label>
          <IdentityPicker
            className="w-full"
            type="user"
            value={createdBy ?? ''}
            onChange={(v) => onCreatedByChange(v || undefined)}
          />
        </div>
      )}
      {onTagsChange && (
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-medium text-muted-foreground">{t('Tags')}</label>
          <TagFilterPicker entityType="applications" permission={permission} value={tags} onChange={onTagsChange} />
          {tags.length > 1 && (
            <span className="text-xs text-muted-foreground">{t('Application must have all selected tags.')}</span>
          )}
        </div>
      )}
      {hasActiveFilter && (
        <div className="flex justify-end pt-1 border-t">
          <Button variant="ghost" size="sm" onClick={clearAll}>
            {t('Clear all')}
          </Button>
        </div>
      )}
    </div>
  );
}

interface ApplicationsFilterProps extends ApplicationsFilterFieldsProps {
  tooltipLabel?: string;
}

export function ApplicationsFilter({ tooltipLabel, ...fieldsProps }: ApplicationsFilterProps) {
  const { t } = useTranslation();
  const hasActiveFilter = !!fieldsProps.environment || !!fieldsProps.createdBy || (fieldsProps.tags?.length ?? 0) > 0;

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
          <p>{tooltipLabel ?? t('Filter applications')}</p>
        </TooltipContent>
      </Tooltip>
      <PopoverContent align="end" className="w-72 p-3">
        <ApplicationsFilterContent {...fieldsProps} />
      </PopoverContent>
    </Popover>
  );
}

export default ApplicationsFilter;
