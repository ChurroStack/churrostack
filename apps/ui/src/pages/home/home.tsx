import '@xyflow/react/dist/style.css';

import { AppSidebar } from '@/components/app-sidebar';
import { useProfile } from '@/hooks/data/profile';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { AppWindow, Brain, Copy, KeyRound, ServerCog } from 'lucide-react';
import { useGetGalleryApps, useGetGalleryLlms } from '@/hooks/data/gallery';
import { useEffect, useMemo, useState } from 'react';
import { renderIcon } from '../../extensions';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import LlmInfoDialog from './dialogs/llm-info-dialog';
import { SearchAndFilter } from '@/components/search-and-filter';
import { ApplicationsFilterContent } from '@/pages/applications/filters/applications-filter';
import { TagBadges } from '@/components/tag-badges';
import { useDebounce } from '@/hooks/use-debounce';

const Home = () => {
  const { profile } = useProfile();
  const { t } = useTranslation();
  const { fetchAsync: fetchGalleryApps, data: galleryApps } = useGetGalleryApps();
  const { fetchAsync: fetchGalleryLlms, data: galleryLlms } = useGetGalleryLlms();
  const [searchValue, setSearchValue] = useState('');
  const debouncedSearch = useDebounce(searchValue, 500);
  const [tagsFilter, setTagsFilter] = useState<string[]>([]);
  const [environmentFilter, setEnvironmentFilter] = useState<string | undefined>(undefined);
  const [createdByFilter, setCreatedByFilter] = useState<string | undefined>(undefined);

  const appsQuery = useMemo(() => {
    const parts: string[] = [];
    if (debouncedSearch) parts.push(`search=${encodeURIComponent(debouncedSearch)}`);
    if (environmentFilter) parts.push(`environment=${encodeURIComponent(environmentFilter)}`);
    if (createdByFilter) parts.push(`createdBy=${encodeURIComponent(createdByFilter)}`);
    for (const tag of tagsFilter) parts.push(`tags=${encodeURIComponent(tag)}`);
    return parts.join('&');
  }, [debouncedSearch, tagsFilter, environmentFilter, createdByFilter]);

  const llmsQuery = useMemo(() => {
    return debouncedSearch ? `search=${encodeURIComponent(debouncedSearch)}` : '';
  }, [debouncedSearch]);

  useEffect(() => {
    fetchGalleryApps(appsQuery);
  }, [fetchGalleryApps, appsQuery]);

  useEffect(() => {
    fetchGalleryLlms(llmsQuery);
  }, [fetchGalleryLlms, llmsQuery]);

  const copyAppUrl = (path: string) => {
    navigator.clipboard.writeText(window.location.origin + '/' + path);
    toast.success(t('Application url copied to clipboard'));
  };

  const hasActiveFilter = tagsFilter.length > 0 || !!environmentFilter || !!createdByFilter;

  return (
    <>
      <div className="absolute top-0 left-0 w-full bg-border h-px z-100"></div>

      <AppSidebar />
      <div className="h-screen overflow-y-auto flex justify-center w-full p-4">
        <div className="w-full max-w-6xl">
          <div className="flex flex-row gap-4 items-start justify-between lg:mt-10">
            <div className="flex flex-col">
              <h1 className="text-2xl font-bold">
                {t('Hello')} {profile?.displayName ?? profile?.name}
              </h1>
              <div>{t('What application do you want to try today?')}</div>
            </div>
            <div className="w-72 shrink-0">
              <SearchAndFilter
                searchValue={searchValue}
                onSearchValueChange={setSearchValue}
                placeholder={t('Search...')}
                hasActiveFilter={hasActiveFilter}
                filterContent={
                  <div className="flex flex-col gap-3">
                    <ApplicationsFilterContent
                      environment={environmentFilter}
                      createdBy={createdByFilter}
                      tags={tagsFilter}
                      permission="execute"
                      onEnvironmentChange={setEnvironmentFilter}
                      onCreatedByChange={setCreatedByFilter}
                      onTagsChange={setTagsFilter}
                    />
                    <span className="text-xs text-muted-foreground border-t pt-2">
                      {t('Filters apply to Applications only.')}
                    </span>
                  </div>
                }
              />
            </div>
          </div>
          <h2 className="text-xl font-bold mt-4 lg:mt-10">{t('Applications')}</h2>
          {galleryApps?.items && galleryApps?.items?.length === 0 && (
            <div className="text-muted-foreground mt-4">
              {t('There are no applications shared with you in the gallery.')}
            </div>
          )}
          <div
            className="
          mt-4
          grid
          grid-cols-1
          sm:grid-cols-2
          lg:grid-cols-3
          xl:grid-cols-4
          gap-4">
            <Skeleton className={`w-full h-20 rounded-md ${galleryApps ? 'hidden' : 'block'}`} />
            {galleryApps?.items?.map((app) => (
              <Link key={`app-${app.name}`} to={app.path} target="_blank">
                <div className="border gap-2 rounded-sm p-2 flex flex-row tems-start min-w-32 md:max-w-100 w-full hover:bg-accent cursor-pointer">
                  {renderIcon(app.icon ?? 'app-window', 'size-6 m-2')}
                  <div className="flex flex-col w-full gap-1 items-start min-w-0 flex-1">
                    <div className="text-xs font-semibold uppercase w-full truncate min-w-0">{app.name}</div>
                    <div className="text-xs text-muted-foreground break-all line-clamp-1">{app.description}</div>
                    <TagBadges tags={app.tags} max={3} />
                  </div>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Button
                        variant="ghost"
                        className="size-4 cursor-pointer"
                        onClick={(e) => {
                          copyAppUrl(app.path);
                          e.stopPropagation();
                          e.preventDefault();
                        }}>
                        <Copy className="size-3" />
                      </Button>
                    </TooltipTrigger>
                    <TooltipContent side="bottom">{t('Copy application URL')}</TooltipContent>
                  </Tooltip>
                </div>
              </Link>
            ))}
          </div>

          <h2 className="text-xl font-bold mt-4 lg:mt-10">{t('Large language models (LLMs)')}</h2>
          {galleryLlms?.items && galleryLlms?.items?.length === 0 && (
            <div className="text-muted-foreground mt-4">{t('There are no LLMs shared with you in the gallery.')}</div>
          )}
          <div
            className="
          mt-4
          grid
          grid-cols-1
          sm:grid-cols-2
          lg:grid-cols-3
          xl:grid-cols-4
          gap-4">
            <Skeleton className={`w-full h-20 rounded-md ${galleryLlms ? 'hidden' : 'block'}`} />
            {galleryLlms?.items?.map((llm) => (
              <LlmInfoDialog llm={llm} key={`llm-info-${llm.id}`}>
                <div
                  key={`llm-${llm.names[0]}`}
                  className="border gap-2 rounded-sm p-2 flex flex-row items-center min-w-32 md:max-w-100 w-full hover:bg-accent cursor-pointer">
                  <Brain className="size-6 m-2" />
                  <div className="text-xs font-semibold uppercase w-full truncate min-w-0">{llm.names[0]}</div>
                </div>
              </LlmInfoDialog>
            ))}
          </div>
          {(profile.canCreateApplications || profile.role == 'administrator') && (
            <h2 className="text-xl font-bold mt-4 lg:mt-10">{t('Quick links')}</h2>
          )}
          {(profile.canCreateApplications || profile.role == 'administrator') && (
            <div className="flex flex-row flex-wrap gap-4 w-full">
              {profile.canCreateApplications && (
                <Link to="/applications">
                  <div className="border gap-2 rounded-sm p-4 flex flex-row items-center mt-4 hover:bg-accent cursor-pointer min-w-40">
                    <AppWindow size={24} />
                    <div className="text-xs font-semibold uppercase">{t('Applications')}</div>
                  </div>
                </Link>
              )}
              {profile.canCreateApplications && (
                <Link to="/environments">
                  <div className="border gap-2 rounded-sm p-4 flex flex-row items-center mt-4 hover:bg-accent cursor-pointer min-w-40">
                    <ServerCog size={24} />
                    <div className="text-xs font-semibold uppercase">{t('Environments')}</div>
                  </div>
                </Link>
              )}
              <Link to="/keys">
                <div className="border gap-2 rounded-sm p-4 flex flex-row items-center mt-4 hover:bg-accent cursor-pointer min-w-40">
                  <KeyRound size={24} />
                  <div className="text-xs font-semibold uppercase">{t('Api Keys')}</div>
                </div>
              </Link>
            </div>
          )}
          <br />
        </div>
      </div>
    </>
  );
};

export default Home;
