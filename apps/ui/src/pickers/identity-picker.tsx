import { useSearchIdentities, type IdentityItem } from '@/hooks/data/identities';
import { AlertCircle, AppWindow, User, Users, X } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { AsyncSelect } from '@/components/async-select';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

const IdentityPicker = ({
  value,
  className,
  onChange,
  autoSelect,
  readonly,
  type,
  clearable
}: {
  className?: string;
  value?: string;
  onChange?: (value: string) => void;
  autoSelect?: boolean;
  readonly?: boolean;
  type?: string;
  clearable?: boolean;
}) => {
  const { t } = useTranslation();
  const { fetchAsync, data, error } = useSearchIdentities();
  const [selectedUser, setSelectedUser] = useState<string>(value ?? '');
  const [edit, setEdit] = useState<boolean>(!selectedUser);

  useEffect(() => {
    setSelectedUser(value ?? '');
    // When value is cleared externally, return to the search input so the user can pick again.
    if (!value) setEdit(true);
  }, [value]);

  useEffect(() => {
    if (autoSelect && !value && data?.items?.length === 1) {
      onChange?.(data.items[0].name);
    }
  }, [autoSelect, data, value]);

  const fetcher = useCallback(
    async (input?: string) => {
      const result = await fetchAsync(`search=${input}`);
      return result?.data?.items ?? [];
    },
    [fetchAsync]
  );

  if (readonly && value) {
    return <div className="flex flex-row gap-2 items-center">{value}</div>;
  }

  if (error)
    return (
      <span className="text-red-500 mr-2">
        <AlertCircle className="w-4 h-4" />
      </span>
    );

  if (!edit) {
    return (
      <div
        className={cn('flex flex-row gap-2 items-center cursor-pointer border rounded-md px-2 pr-2 h-9', className)}
        onClick={() => setEdit(true)}>
        {type == 'user' && <User className="size-4" />}
        {type == 'application' && <AppWindow className="size-4" />}
        {type == 'group' && <Users className="size-4" />}
        <span className="flex-1 truncate">{value}</span>
        {clearable && (
          <button
            type="button"
            className="text-muted-foreground hover:text-foreground"
            aria-label={t('Clear')}
            onClick={(e) => {
              e.stopPropagation();
              setSelectedUser('');
              setEdit(true);
              onChange?.('');
            }}>
            <X className="size-4" />
          </button>
        )}
      </div>
    );
  }
  return (
    <AsyncSelect<IdentityItem>
      triggerClassName={className}
      fetcher={fetcher}
      renderOption={(item) => (
        <div className="flex flex-row gap-2 items-center">
          {item.type == 'user' && <User className="size-4" />}
          {item.type == 'application' && <AppWindow className="size-4" />}
          {item.type == 'group' && <Users className="size-4" />}
          {item.name}
        </div>
      )}
      getOptionValue={(item) => item.name}
      getDisplayValue={(item) => {
        return (
          <div className="flex flex-row gap-2 w-full">
            {item.type == 'user' && <User className="size-4" />}
            {item.type == 'application' && <AppWindow className="size-4" />}
            {item.type == 'group' && <Users className="size-4" />}
            {item.displayName ?? item.name}
          </div>
        );
      }}
      notFound={<div className="py-6 text-center text-sm">{t('No identities found')} </div>}
      label={t('Identity')}
      placeholder={t('Search user, groups or apps...')}
      value={selectedUser}
      onChange={(value) => {
        setSelectedUser(value);
        onChange?.(value);
      }}
    />
  );
};

export default IdentityPicker;
