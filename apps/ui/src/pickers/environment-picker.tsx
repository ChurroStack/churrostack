import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Spinner } from '@/components/ui/spinner';
import { useGetEnvironments } from '@/hooks/data/environments';
import { cn } from '@/lib/utils';
import { AlertCircle, Check, ChevronsUpDown, ServerCog } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const EnvironmentPicker = ({
  value,
  className,
  onChange,
  autoSelect,
  readonly
}: {
  className?: string;
  value: string;
  onChange: (value: string) => void;
  autoSelect?: boolean;
  readonly?: boolean;
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const { fetchAsync, data, error, isFetching } = useGetEnvironments();

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync]);

  useEffect(() => {
    if (autoSelect && !value && data?.items?.length === 1) {
      onChange(data.items[0].name);
    }
  }, [autoSelect, data, value]);

  const selectedValue = useMemo(() => {
    return (data?.items ?? []).find((environment) => environment.name === value);
  }, [data, value]);

  if (readonly && value) {
    return (
      <div className="flex flex-row gap-2 items-center">
        <ServerCog className="size-4" /> {value}
      </div>
    );
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className={cn('min-w-50 justify-between', className)}>
          {error && (
            <span className="text-red-500 mr-2">
              <AlertCircle className="w-4 h-4" />
            </span>
          )}
          {isFetching && <Spinner className="mr-2" />}
          {selectedValue ? (
            <div className="flex flex-row gap-2 items-center">
              <ServerCog className="size-4" /> {selectedValue.name}
            </div>
          ) : (
            t('Select a environment...')
          )}
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="p-0">
        <Command>
          <CommandInput placeholder={t('Search environment...')} className="h-9" />
          <CommandList>
            <CommandEmpty>{t('No environment found.')}</CommandEmpty>
            <CommandGroup>
              {data?.items?.map((environment) => (
                <CommandItem
                  className="flex flex-row gap-2 items-center"
                  key={environment.name}
                  value={environment.name}
                  onSelect={(currentValue) => {
                    onChange?.(currentValue === value ? '' : currentValue);
                    setOpen(false);
                  }}>
                  <ServerCog className="size-4" />
                  {environment.name}
                  <Check className={cn('ml-auto', value === environment.name ? 'opacity-100' : 'opacity-0')} />
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
};

export default EnvironmentPicker;
