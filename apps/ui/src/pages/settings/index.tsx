import { BreadcrumbItem, BreadcrumbLink, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem
} from '@/components/ui/sidebar';
import { MenuLayout } from '@/layouts/menu-layout';
import { Cog, Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router';

export default function AdminPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  return (
    <>
      <MenuLayout
        plain={true}
        breadcrumb={
          <>
            <BreadcrumbItem className="hidden md:block">
              <BreadcrumbLink onClick={() => navigate('/')} href="#">
                Home
              </BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator className="hidden md:block" />
            <BreadcrumbItem>
              <BreadcrumbPage>{t('Settings')}</BreadcrumbPage>
            </BreadcrumbItem>
          </>
        }>
        <SidebarHeader className="gap-3.5 border-b p-4 w-75 h-[61px]">
          <div className="flex w-full items-center justify-between">
            <div className="text-foreground text-base font-medium">{t('Settings')}</div>
            {/* <Label className="flex items-center gap-2 text-sm">
              <span>Unreads</span>
              <Switch className="shadow-none" />
            </Label> */}
          </div>
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup className="p-0">
            <SidebarGroupContent>
              <Sidebar collapsible="none">
                <SidebarContent className="flex flex-col w-75">
                  <SidebarGroup>
                    <SidebarGroupContent>
                      <SidebarMenu>
                        <SidebarMenuItem className="flex flex-col gap-1">
                          <SidebarMenuButton asChild isActive={pathname.endsWith('/general')}>
                            <Link to="/settings/general">
                              <Cog />
                              <span>{t('General')}</span>
                            </Link>
                          </SidebarMenuButton>
                          <SidebarMenuButton asChild isActive={pathname.endsWith('/identities')}>
                            <Link to="/settings/identities">
                              <Users />
                              <span>{t('Identities')}</span>
                            </Link>
                          </SidebarMenuButton>
                        </SidebarMenuItem>
                      </SidebarMenu>
                    </SidebarGroupContent>
                  </SidebarGroup>
                </SidebarContent>
              </Sidebar>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>
      </MenuLayout>
    </>
  );
}
