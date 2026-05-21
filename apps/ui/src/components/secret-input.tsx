import * as React from 'react';
import { Check, Copy } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { toast } from 'sonner';
import { useTranslation } from 'react-i18next';

interface SecretInputProps {
  value: string;
}

export function SecretInput({ value }: SecretInputProps) {
  const [copied, setCopied] = React.useState(false);
  const { t } = useTranslation();

  const handleCopy = async () => {
    await navigator.clipboard.writeText(value);
    toast.success(t('Copied to clipboard!'));
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <div className="flex items-center gap-2">
      <Input className="w-full" value={value} readOnly />
      <Button type="button" variant="outline" size="icon" onClick={handleCopy} title={t('Copy')}>
        {copied ? <Check className="h-4 w-4" color="green" /> : <Copy className="h-4 w-4" />}
      </Button>
    </div>
  );
}
