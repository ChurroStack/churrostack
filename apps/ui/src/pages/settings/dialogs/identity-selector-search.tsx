'use client';
import { useEffect, useState } from 'react';
import { Alert, AlertTitle } from '@/components/ui/alert';
import { AlertCircle } from 'lucide-react';
import { getUserIcon, type IdentityItem, type IdentityType, useSearchIdentities } from '@/hooks/data/identities';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { useDebounce } from 'use-debounce';
import { type QueryResult } from '@/hooks/data/core';
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import IdentityAvatar from '@/components/identity-avatar';
import { Spinner } from '@/components/ui/spinner';

// The component props you want to require
export interface IdentitySelectorSearchProps {
  className?: string;
  onSelect: (value: IdentityItem) => void;
  filterType?: IdentityType[];
  placeholder?: string;
}

// The model (M) in M-V-VM (logic, behavior side effects)
export function useIdentitySelectorSearchModel({ onSelect, filterType }: IdentitySelectorSearchProps) {
  const { isFetching, error, data, fetchAsync } = useSearchIdentities();
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearchQuery] = useDebounce(searchQuery, 500);
  const [queryString, setQueryString] = useState<string>('');

  useEffect(() => {
    let queryFilter = '';
    if (filterType) {
      queryFilter = `&type=${filterType.join(',')}`;
    }

    if (debouncedSearchQuery) {
      setQueryString(`search=${debouncedSearchQuery ?? ''}${queryFilter}`);
    } else {
      setQueryString(queryFilter);
    }
  }, [debouncedSearchQuery, filterType]);

  useEffect(() => {
    if (queryString) {
      fetchAsync(queryString);
    } else {
      fetchAsync('');
    }
  }, [queryString]);

  const onClick = (identityName: IdentityItem) => {
    onSelect(identityName);
    setSearchQuery('');
  };

  return {
    isFetching,
    error,
    data,
    onSelect,
    searchQuery,
    setSearchQuery,
    getUserIcon,
    onClick
  };
}

// The pure view (V) in M-V-VM (no logic, no side effects)
export default function IdentitySelectorSearchView({
  isFetching,
  error,
  data,
  searchQuery,
  setSearchQuery,
  className,
  onClick,
  placeholder
}: IdentitySelectorSearchProps & {
  error?: string;
  data?: QueryResult<IdentityItem>;
  isFetching: boolean;
  searchQuery: string;
  setSearchQuery: (query: string) => void;
  onClick: (identityName: IdentityItem) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <div className={className}>
      {error && (
        <Alert variant="destructive" className="p-2 m-4 flex">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Something went wrong. Try again...')}</AlertTitle>
        </Alert>
      )}
      <div className="flex gap-2">
        <div className="flex-1">
          <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
              <div className="flex items-center gap-2 text-accent-foreground text-sm py-1">
                <Input
                  placeholder={placeholder ?? t('Add people or groups')}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>
            </PopoverTrigger>
            <PopoverContent className="w-[300px] p-0" onOpenAutoFocus={(e) => e.preventDefault()}>
              <div className="flex flex-col p-1">
                {(isFetching || data?.items?.length === 0) && (
                  <div className="flex items-center justify-center text-accent-foreground text-sm">
                    {isFetching ? <Spinner /> : t('No results found.')}
                  </div>
                )}
                <div className="flex flex-col max-h-[400px] overflow-y-auto">
                  {data?.items?.map((identity) => (
                    <button
                      type="button"
                      className="flex justify-between items-center gap-2 cursor-pointer p-2 hover:bg-accent rounded-md w-full text-left"
                      key={identity.name}
                      onClick={() => {
                        onClick(identity);
                        setOpen(false);
                      }}>
                      <div className="flex items-center gap-2 text-accent-foreground text-sm">
                        <IdentityAvatar name={identity.name} type={identity.type} size={32} />
                        <div className="grid flex-1 text-left text-sm leading-tight">
                          <span className="truncate font-semibold">{identity.displayName ?? identity.name}</span>
                          <span className="truncate text-xs">{identity.name}</span>
                        </div>
                      </div>
                    </button>
                  ))}
                </div>
              </div>
            </PopoverContent>
          </Popover>
        </div>
      </div>
    </div>
  );
}

// The ViewModel (VM) or final component in M-V-VM (composition of M and V)
export function IdentitySelectorSearch(props: IdentitySelectorSearchProps) {
  const model = useIdentitySelectorSearchModel(props);

  return <IdentitySelectorSearchView {...props} {...model} />;
}
