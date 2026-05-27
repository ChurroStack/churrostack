import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Spinner } from '@/components/ui/spinner';
import { useGetTags, type TagEntityType, type TagPermission } from '@/hooks/data/tags';
import { cn } from '@/lib/utils';
import { useNotifications } from '@/services/notification-service';
import { AlertCircle, Check, ChevronsUpDown, Tag, X } from 'lucide-react';
import { useEffect, useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';

interface TagFilterPickerProps {
  entityType: TagEntityType;
  permission?: TagPermission;
  value: string[];
  onChange: (next: string[]) => void;
  className?: string;
}

const TagFilterPicker = ({ entityType, permission = 'read', value, onChange, className }: TagFilterPickerProps) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const { fetchAsync, data, error, isFetching } = useGetTags(entityType);
  const { subscribe } = useNotifications();

  useEffect(() => {
    if (open) {
      fetchAsync(`permission=${permission}`);
    }
  }, [open, permission, fetchAsync]);

  // When the underlying entities are modified (potentially adding/removing tags), refetch
  // the tag list while the popover is open so the dropdown isn't stale.
  useEffect(() => {
    const target = entityType === 'applications' ? 'application' : 'environment';
    return subscribe(() => {
      if (open) fetchAsync(`permission=${permission}`);
    }, [target]);
  }, [entityType, open, permission, fetchAsync, subscribe]);

  const toggle = (tag: string) => {
    if (value.includes(tag)) {
      onChange(value.filter((t) => t !== tag));
    } else {
      onChange([...value, tag]);
    }
  };

  const removeTag = (tag: string, e: React.MouseEvent | KeyboardEvent) => {
    e.stopPropagation();
    e.preventDefault();
    onChange(value.filter((t) => t !== tag));
  };

  const onChipKeyDown = (tag: string, e: KeyboardEvent<HTMLSpanElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      removeTag(tag, e);
    }
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className={cn('w-full min-h-9 h-auto justify-between flex-wrap gap-1 py-1', className)}>
          <div className="flex flex-row flex-wrap gap-1 items-center min-w-0 flex-1">
            {error && <AlertCircle className="size-4 text-red-500" />}
            {isFetching && <Spinner className="size-3" />}
            {value.length === 0 ? (
              <span className="text-muted-foreground flex flex-row items-center gap-2">
                <Tag className="size-3" /> {t('Select tags...')}
              </span>
            ) : (
              value.map((tag) => (
                <span
                  key={tag}
                  className="inline-flex items-center gap-1 rounded-sm bg-secondary px-1.5 py-0.5 text-xs">
                  <Tag className="size-3" />
                  <span className="max-w-40 truncate">{tag}</span>
                  <span
                    role="button"
                    tabIndex={0}
                    aria-label={t('Remove tag')}
                    onClick={(e) => removeTag(tag, e)}
                    onKeyDown={(e) => onChipKeyDown(tag, e)}
                    className="ml-0.5 hover:text-foreground cursor-pointer">
                    <X className="size-3" />
                  </span>
                </span>
              ))
            )}
          </div>
          <ChevronsUpDown className="opacity-50 shrink-0" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="p-0" align="start">
        <Command>
          <CommandInput placeholder={t('Search tag...')} className="h-9" />
          <CommandList>
            <CommandEmpty>{t('No tags yet.')}</CommandEmpty>
            <CommandGroup>
              {(data ?? []).map((tag) => {
                const checked = value.includes(tag);
                return (
                  <CommandItem
                    className="flex flex-row gap-2 items-center"
                    key={tag}
                    value={tag}
                    onSelect={() => toggle(tag)}>
                    <Tag className="size-4" />
                    <span className="truncate">{tag}</span>
                    <Check className={cn('ml-auto', checked ? 'opacity-100' : 'opacity-0')} />
                  </CommandItem>
                );
              })}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
};

export default TagFilterPicker;
