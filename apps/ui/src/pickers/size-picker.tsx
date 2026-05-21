import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Spinner } from '@/components/ui/spinner';
import type { ApplicationSize } from '@/hooks/data/applications';
import { useGetEnvironment } from '@/hooks/data/environments';
import { cn } from '@/lib/utils';
import { AlertCircle, Check, ChevronsUpDown, Server } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const EnvironmentSizePicker = ({
  environmentName,
  value,
  onChange,
  autoSelect,
  readonly
}: {
  environmentName: string;
  value?: ApplicationSize;
  onChange?: (value: ApplicationSize) => void;
  autoSelect?: boolean;
  readonly?: boolean;
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const { fetchAsync, data, error, isFetching } = useGetEnvironment(environmentName);

  const sizes = useMemo(() => {
    return (
      data?.definition?.sizes.map((o) => ({
        title: o.title,
        size: {
          hint: o.name,
          cpu: (o.limits ?? o.requests).cpu,
          memory: (o.limits ?? o.requests).memory,
          gpu: (o.limits ?? o.requests).gpu,
          storage: (o.limits ?? o.requests).storage
        } as ApplicationSize
      })) ?? []
    );
  }, [data]);

  useEffect(() => {
    fetchAsync('');
  }, [fetchAsync, environmentName]);

  useEffect(() => {
    if (autoSelect && !(sizes?.length === 1) && value === undefined) {
      onChange?.(sizes[0].size);
    }
  }, [autoSelect, sizes]);

  const selectedValue = useMemo(() => {
    let size = (sizes ?? []).find((environment) => environment.size.hint === value?.hint);
    if (!size) {
      size = (sizes ?? []).find(
        (environment) =>
          environment.size.cpu === value?.cpu &&
          environment.size.memory === value?.memory &&
          environment.size.gpu === value?.gpu &&
          environment.size.storage === value?.storage
      );
    }
    return size;
  }, [sizes, value]);

  if (readonly && selectedValue) {
    return (
      <div className="flex flex-row gap-2 items-center">
        <Server className="size-4" /> {}
      </div>
    );
  }

  return (
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
            <div className="flex flex-row gap-2 items-center">
              <Server className="size-4" /> {selectedValue.title}
            </div>
          ) : (
            t('Select a size...')
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
              {sizes?.map((item) => (
                <CommandItem
                  className="flex flex-row gap-2 items-center"
                  key={item.size.hint}
                  value={item.size.hint}
                  onSelect={(currentValue) => {
                    onChange?.(sizes.find((env) => env.size.hint === currentValue)!.size);
                    setOpen(false);
                  }}>
                  <Server className="size-4" />
                  {item.title}
                  <Check className={cn('ml-auto', value === item.size.hint ? 'opacity-100' : 'opacity-0')} />
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
};

export default EnvironmentSizePicker;
