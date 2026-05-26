import { LoadingSkeleton } from '@/components/loading-skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { useGetLlm } from '@/hooks/data/llms';
import { AlertCircle, Brain, ChartNoAxesCombined, Cog, Plug, Sparkles, UserLock } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import SettingsPanel from './panels/settings-panel';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import AccessPanel from './panels/members-panel';
import ConnectionPanel from './panels/connection-panel';
import MonitorPanel from './panels/monitor-panel';
import PlaygroundPanel from './panels/playground-panel';

const Llm = () => {
  const { t } = useTranslation();
  const { id } = useParams();
  const { fetchAsync, data, isFetching, error } = useGetLlm(id ?? '');
  const [llm, setLlm] = useState(data);

  useEffect(() => {
    fetchAsync('').then((response) => {
      setLlm(response?.data);
    });
  }, [id]);

  if (isFetching || !llm) {
    return <LoadingSkeleton maxCards={9} />;
  }

  if (error) {
    return (
      <Alert className="mb-4" variant="destructive">
        <AlertCircle className="size-4" />
        <AlertTitle>{t('Error loading llms')}</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="flex flex-col min-h-0 w-full h-full">
      <div className="flex flex-row justify-between px-4 py-2 pb-0 max-w-full items-center">
        <h1 className="text-2xl font-bold flex flex-row items-center gap-2">
          <Brain /> {llm?.names?.length && llm?.names.length > 0 ? llm.names[0] : llm.id}
        </h1>
        <div className="flex flex-row items-center gap-2"></div>
      </div>
      <Separator className="my-2" />
      <div className="flex flex-col gap-4 p-4 pt-0 min-h-0 w-full h-full">
        {error && (
          <div className="p-2">
            <Alert variant="destructive">
              <AlertCircle className="size-4" />
              <AlertTitle>{t('Error loading LLM')}</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          </div>
        )}
        <Tabs defaultValue="connect" className="flex flex-col min-h-0 w-full h-full">
          <TabsList>
            <TabsTrigger value="connect">
              <div className="flex flex-row items-center gap-2 px-2">
                <Plug /> {t('Connection information')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="playground">
              <div className="flex flex-row items-center gap-2 px-2">
                <Sparkles /> {t('Playground')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="monitoring">
              <div className="flex flex-row items-center gap-2 px-2">
                <ChartNoAxesCombined /> {t('Monitoring')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="settings">
              <div className="flex flex-row items-center gap-2 px-2">
                <Cog /> {t('General Settings')}
              </div>
            </TabsTrigger>
            <TabsTrigger value="security">
              <div className="flex flex-row items-center gap-2 px-2">
                <UserLock /> {t('Manage Access')}
              </div>
            </TabsTrigger>
          </TabsList>
          <TabsContent value="settings" className="flex flex-col min-h-0 w-full h-full">
            <SettingsPanel llm={llm} />
          </TabsContent>
          <TabsContent value="connect" className="flex flex-col min-h-0 w-full h-full">
            <ConnectionPanel llm={llm} />
          </TabsContent>
          <TabsContent value="playground" className="flex flex-col min-h-0 w-full h-full">
            <PlaygroundPanel llm={llm} />
          </TabsContent>
          <TabsContent value="monitoring" className="flex flex-col min-h-0 w-full h-full">
            <MonitorPanel llm={llm} />
          </TabsContent>
          <TabsContent value="security" className="flex flex-col min-h-0 w-full h-full">
            <AccessPanel llm={llm} />
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
};

export default Llm;
