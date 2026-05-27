import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { Tag, X } from 'lucide-react';
import { useRef, useState, type FocusEvent, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';

interface TagChipsInputProps {
  value: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
}

const TAG_PATTERN = /^[a-z0-9_-]{1,32}$/;

function normalize(input: string): string {
  return input.trim().toLowerCase();
}

export function TagChipsInput({ value, onChange, placeholder, className, disabled }: TagChipsInputProps) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);

  const onBlur = (e: FocusEvent<HTMLInputElement>) => {
    // Don't commit when focus moved to one of our own chip-remove buttons —
    // the user is mid-edit, not finishing the draft.
    if (containerRef.current && e.relatedTarget && containerRef.current.contains(e.relatedTarget as Node)) {
      return;
    }
    commit(draft);
  };

  const commit = (raw: string) => {
    const normalized = normalize(raw);
    if (!normalized) return;
    if (!TAG_PATTERN.test(normalized)) {
      setError(t('Tags must match a-z, 0-9, _, - (max 32 chars).'));
      return;
    }
    if (value.includes(normalized)) {
      setDraft('');
      return;
    }
    onChange([...value, normalized]);
    setDraft('');
    setError(null);
  };

  const remove = (tag: string) => {
    onChange(value.filter((t) => t !== tag));
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      commit(draft);
    } else if (e.key === 'Backspace' && draft === '' && value.length > 0) {
      e.preventDefault();
      remove(value[value.length - 1]);
    }
  };

  return (
    <div className={cn('flex flex-col gap-1', className)}>
      <div ref={containerRef} className="flex flex-row flex-wrap gap-1 p-2 border rounded-md bg-background min-h-9 items-center">
        {value.map((tag) => (
          <span
            key={tag}
            className="inline-flex items-center gap-1 rounded-sm bg-secondary px-1.5 py-0.5 text-xs">
            <Tag className="size-3" />
            <span className="max-w-40 truncate">{tag}</span>
            {!disabled && (
              <button
                type="button"
                aria-label={t('Remove tag')}
                onClick={() => remove(tag)}
                className="ml-0.5 hover:text-foreground">
                <X className="size-3" />
              </button>
            )}
          </span>
        ))}
        <Input
          className="border-0 shadow-none h-7 flex-1 min-w-32 focus-visible:ring-0 px-1"
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            setError(null);
          }}
          onKeyDown={onKeyDown}
          onBlur={onBlur}
          placeholder={value.length === 0 ? placeholder ?? t('Type a tag and press Enter') : ''}
          disabled={disabled}
        />
      </div>
      {error && <span className="text-xs text-destructive">{error}</span>}
    </div>
  );
}

export default TagChipsInput;
