import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Spinner } from '@/components/ui/spinner';
import { renderIcon } from '@/extensions';
import { useGetTemplates } from '@/hooks/data/templates';
import { cn } from '@/lib/utils';
import { AlertCircle, Check, ChevronsUpDown } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const TemplatePicker = ({
  value,
  className,
  onChange,
  autoSelect
}: {
  className?: string;
  value: string;
  onChange: (value: string) => void;
  autoSelect?: boolean;
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const { fetchAsync, data, error, isFetching } = useGetTemplates();

  useEffect(() => {
    fetchAsync('type=application');
  }, [fetchAsync]);

  useEffect(() => {
    if (autoSelect && !value && data?.items?.length === 1) {
      onChange(data.items[0].name);
    }
  }, [autoSelect, data, value]);

  const selectedValue = useMemo(() => {
    return (data?.items ?? []).find((template) => template.name === value);
  }, [data, value]);

  return (
    <div className={cn('flex flex-col gap-2', className)}>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button variant="outline" role="combobox" aria-expanded={open} className="min-w-50 justify-between">
            {error && (
              <span className="text-red-500 mr-2">
                <AlertCircle className="w-4 h-4" />
              </span>
            )}
            {isFetching && <Spinner className="mr-2" />}
            {selectedValue ? (
              <div className="flex flex-row gap-2">
                {renderIcon(selectedValue.icon, 'size-4')} {selectedValue.title}
              </div>
            ) : (
              t('Select a template...')
            )}
            <ChevronsUpDown className="opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="p-0">
          <Command>
            <CommandInput placeholder={t('Search template...')} className="h-9" />
            <CommandList>
              <CommandEmpty>{t('No template found.')}</CommandEmpty>
              <CommandGroup>
                {data?.items?.map((template) => (
                  <CommandItem
                    className="flex flex-row gap-2 items-center"
                    key={template.name}
                    value={template.name}
                    onSelect={(currentValue) => {
                      onChange?.(currentValue === value ? '' : currentValue);
                      setOpen(false);
                    }}>
                    {renderIcon(template.icon, 'size-4')}
                    {template.title}
                    <Check className={cn('ml-auto', value === template.name ? 'opacity-100' : 'opacity-0')} />
                  </CommandItem>
                ))}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {selectedValue && <p className="text-sm text-muted-foreground">{selectedValue.description}</p>}
    </div>
  );
};

export default TemplatePicker;
