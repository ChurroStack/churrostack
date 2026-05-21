'use client';

import * as React from 'react';
import { AppWindow, Brain, Cog, FileBraces, ServerCog, KeyRound } from 'lucide-react';

import { NavUser } from '@/components/nav-user';
import { ModeToggle } from '@/components/mode-toggle';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarInput,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar
} from '@/components/ui/sidebar';
import { Link, useLocation, useNavigate } from 'react-router';
import { useProfile } from '@/hooks/data/profile';
import { getInitials } from '@/extensions';
export interface AppSidebarProps extends React.ComponentProps<typeof Sidebar> {
  children?: React.ReactNode;
  buttons?: React.ReactNode;
  searchPlaceholder?: string;
  plain?: boolean;
  searchValue?: string;
  onSearchValueChange?: (value: string) => void;
}

export function AppSidebar({ ...props }: AppSidebarProps) {
  // Note: I'm using state to show active item.
  // IRL you should use the url/router.
  const location = useLocation();
  const { setOpen } = useSidebar();
  const navigate = useNavigate();

  const { profile } = useProfile();

  const menu = React.useMemo(() => {
    const items = [
      {
        title: 'Keys',
        url: '/keys',
        icon: KeyRound
      }
    ];
    if (profile?.canCreateApplications) {
      items.push({
        title: 'Applications',
        url: '/applications',
        icon: AppWindow
      });
      items.push({
        title: 'LLMs',
        url: '/llms',
        icon: Brain
      });
      items.push({
        title: 'Environments',
        url: '/environments',
        icon: ServerCog
      });
    }
    if (profile?.role === 'administrator') {
      items.push({
        title: 'Templates',
        url: '/templates',
        icon: FileBraces
      });
      items.push({
        title: 'Settings',
        url: '/settings/general',
        icon: Cog
      });
    }
    return items;
  }, [profile?.role, profile?.canCreateApplications]);

  const activeItem = React.useMemo(() => {
    let item = menu.find((item) => item.url != '/' && location.pathname.startsWith(item.url));
    if (!item) {
      item = menu[0];
    }
    return item;
  }, [location.pathname, menu]);

  React.useEffect(() => {
    setOpen(!!props?.children);
  }, [props?.children]);

  return (
    <Sidebar collapsible="icon" className="overflow-hidden *:data-[sidebar=sidebar]:flex-row" {...props}>
      {/* This is the first sidebar */}
      {/* We disable collapsible and adjust width to icon. */}
      {/* This will make the sidebar appear as icons. */}
      <Sidebar collapsible="none" className="w-[calc(var(--sidebar-width-icon)+1px)]! border-r">
        <SidebarHeader className="mt-1.5">
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton size="lg" asChild className="md:h-8 md:p-0 pt-4">
                <Link to="/">
                  <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
                    <img src="/icons/icon_light.png" alt="Logo" className="size-6 dark:hidden" />
                    <img src="/icons/icon_dark.png" alt="Logo" className="size-6 hidden dark:block" />
                  </div>
                  {/* <div className="grid flex-1 text-left text-sm leading-tight">
                    <span className="truncate font-medium">Acme Inc</span>
                    <span className="truncate text-xs">Enterprise</span>
                  </div> */}
                </Link>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupContent className="px-1.5 md:px-0">
              <SidebarMenu>
                {menu.map((item) => (
                  <SidebarMenuItem key={item.title}>
                    <SidebarMenuButton
                      tooltip={{
                        children: item.title,
                        hidden: false
                      }}
                      onClick={() => {
                        setOpen(true);
                        navigate(item.url);
                      }}
                      isActive={activeItem?.title === item.title}
                      className="px-2.5 md:px-2">
                      <item.icon />
                      <span>{item.title}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>
        <SidebarFooter>
          <ModeToggle />
          <NavUser
            user={{
              name: profile?.displayName ?? '',
              email: profile?.name ?? '',
              avatar: getInitials(profile?.displayName ?? profile?.name ?? '')
            }}
          />
        </SidebarFooter>
      </Sidebar>

      {/* This is the second sidebar */}
      {/* We disable collapsible and let it fill remaining space */}
      {props?.plain && props?.children && (
        <Sidebar collapsible="none" className="hidden flex-1 md:flex">
          {props?.children}
        </Sidebar>
      )}
      {!props?.plain && props?.children && (
        <Sidebar collapsible="none" className="hidden flex-1 md:flex">
          <SidebarHeader className="border-b p-0 gap-0">
            <div className="flex w-full items-center justify-between p-4.5 border-b">
              <div className="text-foreground uppercase font-medium">{activeItem?.title}</div>
              <div className="flex flex-row gap-2 p-0">{props?.buttons}</div>
            </div>
            <div className="p-2">
              <SidebarInput
                placeholder={props?.searchPlaceholder ?? 'Type to search...'}
                value={props?.searchValue}
                onChange={(e) => props?.onSearchValueChange?.(e.target.value)}
              />
            </div>
          </SidebarHeader>
          <SidebarContent>
            <SidebarGroup className="p-0">
              <SidebarGroupContent>{props?.children}</SidebarGroupContent>
            </SidebarGroup>
          </SidebarContent>
        </Sidebar>
      )}
    </Sidebar>
  );
}
