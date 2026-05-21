import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { isNullOrWhiteSpace } from '@/extensions';
import type { ApplicationItem } from '@/hooks/data/applications';
import { useStreamingSse } from '@/hooks/data/core';
import { AlertCircle, AlertCircleIcon, Check, Unplug } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

const ConsolePanel = ({ app }: { app: ApplicationItem }) => {
  const { t } = useTranslation();
  const [deploymentName, setDeploymentName] = useState<string>(app?.deployments?.[0]?.name || '');
  const { fetchAsync, error, isFetching, controller } = useStreamingSse<string>(
    `/api/applications/${app.name}/console/${deploymentName}`
  );
  const [lines, setLines] = useState<string[]>([]);
  const [scrollToBottom, setScrollToBottom] = useState<boolean>(true);
  const bottomRef = useRef<HTMLDivElement | null>(null);

  const updateLine = (newLine: string) => {
    setLines((prevLines) => [...prevLines, newLine]);
    if (scrollToBottom) {
      bottomRef?.current?.scrollIntoView({ behavior: 'smooth' });
    }
  };

  useEffect(() => {
    setLines([]);
    fetchAsync((data) => {
      updateLine(data);
    });
    return () => {
      controller?.abort();
    };
  }, [app?.name, deploymentName]);

  if (isNullOrWhiteSpace(deploymentName)) {
    return (
      <Alert>
        <AlertCircle className="size-4" />
        <AlertTitle>{t('No deployment found')}</AlertTitle>
        <AlertDescription>{t('Please select a deployment to view the console output.')}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="overflow-hidden rounded-md border flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between py-2 px-2 ">
        <div className="flex flex-row items-center">
          {isFetching && (
            <Switch
              id="scroll-to-bottom"
              className="mr-2"
              checked={scrollToBottom}
              onCheckedChange={(checked) => setScrollToBottom(checked)}
            />
          )}
          {isFetching && (
            <label htmlFor="scroll-to-bottom" className="select-none mr-4 text-muted-foreground text-sm">
              {t('Scroll to Bottom automatically')}
            </label>
          )}
          {!isFetching && (
            <div className="flex flex-row gap-2 text-sm items-center">
              <AlertCircleIcon className="text-yellow-600" />
              <span className="text-yellow-600">{t('Console is disconnected')}</span>
            </div>
          )}
          {/* {lastUpdate && (
            <span className="text-muted-foreground text-sm">
              {t('Updated')} {lastUpdate}
            </span>
          )} */}
        </div>
        <div className="flex flex-row gap-2">
          <Select value={deploymentName} onValueChange={(value) => setDeploymentName(value)}>
            <SelectTrigger className="flex-1 py-1 data-[size=default]:h-8">
              <SelectValue placeholder={t('Select a deployment...')} />
            </SelectTrigger>
            <SelectContent>
              <SelectGroup>
                <SelectLabel>{t('Deployments')}</SelectLabel>
                {app?.deployments?.map((deployment) => (
                  <SelectItem key={deployment.name} value={deployment.name}>
                    {deployment.name}
                  </SelectItem>
                ))}
              </SelectGroup>
            </SelectContent>
          </Select>
          {isFetching && (
            <span className="flex flex-row gap-2 items-center text-sm text-muted-foreground">
              <Check /> {t('Connected')}
            </span>
          )}
          {!isFetching && (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                fetchAsync((data) => {
                  updateLine(data);
                });
              }}
              disabled={isFetching}>
              <Unplug /> {t('Connect')}
            </Button>
          )}
        </div>
      </div>
      {error && (
        <Alert className="mb-4" variant="destructive">
          <AlertCircle className="size-4" />
          <AlertTitle>{t('Error loading applications')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
      <div className="font-terminal bg-neutral-950 text-gray-300 px-2 w-full h-full overflow-auto">
        {lines.map((line, index) => (
          <div key={`line-${index}`}>{line}</div>
        ))}
        <div ref={bottomRef} className="mt-10" />
      </div>
    </div>
  );
};

export default ConsolePanel;
