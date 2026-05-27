import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { Filter, Search } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

// Reusable search + filter shell for sections that compose their own popover content.
// Used on the Home page and Environment → Applications tab. The Applications sidebar
// intentionally keeps `MenuLayout`'s built-in search field + `ApplicationsFilter` button
// because the sidebar's header layout is owned by `MenuLayout` rather than by the page.
interface SearchAndFilterProps {
  searchValue: string;
  onSearchValueChange: (value: string) => void;
  filterContent?: ReactNode;
  hasActiveFilter?: boolean;
  activeBadges?: ReactNode;
  placeholder?: string;
  className?: string;
}

export function SearchAndFilter({
  searchValue,
  onSearchValueChange,
  filterContent,
  hasActiveFilter,
  activeBadges,
  placeholder,
  className
}: SearchAndFilterProps) {
  const { t } = useTranslation();

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <div className="flex flex-row gap-2 items-center">
        <div className="relative flex-1 min-w-0">
          <Search className="absolute left-2 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
          <Input
            value={searchValue}
            onChange={(e) => onSearchValueChange(e.target.value)}
            placeholder={placeholder ?? t('Search...')}
            className="pl-8 h-8"
          />
        </div>
        {filterContent && (
          <Popover>
            <Tooltip>
              <TooltipTrigger asChild>
                <PopoverTrigger asChild>
                  <Button size="icon" className="size-8 relative shrink-0" variant="secondary" aria-label={t('Filter')}>
                    <Filter />
                    {hasActiveFilter && (
                      <span className="absolute -top-0.5 -right-0.5 size-2 rounded-full bg-primary" />
                    )}
                  </Button>
                </PopoverTrigger>
              </TooltipTrigger>
              <TooltipContent>
                <p>{t('Filter')}</p>
              </TooltipContent>
            </Tooltip>
            <PopoverContent align="end" className="w-72 p-3">
              {filterContent}
            </PopoverContent>
          </Popover>
        )}
      </div>
      {activeBadges}
    </div>
  );
}

export default SearchAndFilter;
