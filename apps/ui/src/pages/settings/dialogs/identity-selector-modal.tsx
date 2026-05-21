'use client';
import { useEffect, useState } from 'react';
import { Alert, AlertTitle } from '@/components/ui/alert';
import { AlertCircle, Check, Search } from 'lucide-react';
import { getUserIcon, type IdentityItem, useSearchIdentities } from '@/hooks/data/identities';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useDebounce } from 'use-debounce';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger
} from '@/components/ui/dialog';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { getInitials } from '@/extensions';
import { Separator } from '@/components/ui/separator';
import { type QueryResult } from '@/hooks/data/core';
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';

// The component props you want to require
export interface IdentitySelectorModalProps {
  className?: string;
  onSelect: (values: IdentityItem[]) => void;
  children: React.ReactNode;
}

// The model (M) in M-V-VM (logic, behavior side effects)
export function useIdentitySelectorModalModel({ onSelect, children }: IdentitySelectorModalProps) {
  const { isFetching, error, data, fetchAsync } = useSearchIdentities();
  const [internalValue, setInternalValue] = useState<IdentityItem[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearchQuery] = useDebounce(searchQuery, 500);

  useEffect(() => {
    if (debouncedSearchQuery) {
      fetchAsync(`search=${debouncedSearchQuery ?? ''}`);
    } else {
      fetchAsync('');
    }
  }, [debouncedSearchQuery]);

  const isSelected = (identity: IdentityItem): boolean => {
    return internalValue.findIndex((item) => item.name === identity.name) !== -1;
  };

  const onSelectIdentity = (identity: IdentityItem) => {
    const copy = [...internalValue];
    const index = copy.findIndex((item) => item.name === identity.name);
    if (index === -1) {
      copy.push(identity);
    } else {
      copy.splice(index, 1);
    }
    setInternalValue(copy);
  };

  const onAdd = () => {
    if (internalValue.length > 0) {
      onSelect(internalValue);
    }
    onClose();
  };

  const onClose = () => {
    setSearchQuery('');
    setInternalValue([]);
  };

  return {
    children,
    isFetching,
    error,
    data,
    onAdd,
    searchQuery,
    setSearchQuery,
    getUserIcon,
    onSelectIdentity,
    isSelected,
    internalValue,
    onClose
  };
}

// The pure view (V) in M-V-VM (no logic, no side effects)
export default function IdentitySelectorModalView({
  children,
  isFetching,
  error,
  data,
  onAdd,
  searchQuery,
  setSearchQuery,
  getUserIcon,
  onSelectIdentity,
  className,
  isSelected,
  internalValue,
  onClose
}: IdentitySelectorModalProps & {
  children: React.ReactNode;
  error?: string;
  data?: QueryResult<IdentityItem>;
  isFetching: boolean;
  searchQuery: string;
  onAdd: () => void;
  setSearchQuery: (query: string) => void;
  getUserIcon: (identity: IdentityItem, className?: string) => React.ReactNode;
  onSelectIdentity: (identity: IdentityItem) => void;
  isSelected: (identity: IdentityItem) => boolean;
  internalValue: IdentityItem[];
  onClose: () => void;
}) {
  const { t } = useTranslation();

  return (
    <div className={className}>
      <Dialog defaultOpen={false} onOpenChange={onClose}>
        <DialogTrigger asChild>{children}</DialogTrigger>
        <DialogContent className="p-4">
          <DialogHeader>
            <DialogTitle>{t('Select identities')}</DialogTitle>
            <DialogDescription>{t('Select the identities you want to add as members')}</DialogDescription>
          </DialogHeader>
          <>
            {error && (
              <Alert variant="destructive" className="p-2 flex">
                <AlertCircle className="size-4" />
                <AlertTitle>{t('Something went wrong. Try again...')}</AlertTitle>
              </Alert>
            )}
            <Separator />

            <div className="flex flex-col gap-2">
              <div className="flex items-center gap-2 text-accent-foreground text-sm">
                <Search className="size-4" />
                <Input
                  className="border-0 shadow-none"
                  placeholder={t('Search identity...')}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>
              <Separator />
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
                    onClick={() => onSelectIdentity(identity)}
                    aria-pressed={isSelected(identity)}>
                    <div className="flex items-center gap-2 text-accent-foreground text-sm">
                      {getUserIcon(identity, 'size-4')}
                      {identity.displayName}
                    </div>
                    <Check className={cn('ml-auto size-4', isSelected(identity) ? 'opacity-100' : 'opacity-0')} />
                  </button>
                ))}
              </div>
            </div>
          </>
          <Separator />
          <DialogFooter>
            <div className="flex w-full justify-between items-center gap-2">
              <div className="flex justify-start">
                {internalValue?.length === 0 && (
                  <span className="text-xs text-gray-500">{t('Select identities to add as members.')}</span>
                )}
                {internalValue?.length > 0 &&
                  internalValue.map((identity) => (
                    <Avatar className="size-6 mr-2" key={identity.name}>
                      <AvatarFallback>{getInitials(identity.displayName)}</AvatarFallback>
                    </Avatar>
                  ))}
              </div>
              <DialogClose asChild>
                <Button disabled={internalValue?.length === 0} onClick={onAdd} type="submit">
                  {t('Add')}
                </Button>
              </DialogClose>
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

// The ViewModel (VM) or final component in M-V-VM (composition of M and V)
export function IdentitySelectorModalContent(props: IdentitySelectorModalProps) {
  const model = useIdentitySelectorModalModel(props);

  return <IdentitySelectorModalView {...props} {...model} />;
}

export function IdentitySelectorModal(props: IdentitySelectorModalProps) {
  return <IdentitySelectorModalContent {...props} />;
}
