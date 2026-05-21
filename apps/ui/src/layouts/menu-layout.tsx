import { AppSidebar } from '@/components/app-sidebar';
import { Breadcrumb, BreadcrumbList } from '@/components/ui/breadcrumb';
import { Separator } from '@/components/ui/separator';
import { SidebarInset, SidebarTrigger } from '@/components/ui/sidebar';
import { Outlet } from 'react-router';

export function MenuLayout({
  breadcrumb,
  children,
  buttons,
  plain,
  searchValue,
  onSearchValueChange
}: {
  breadcrumb: React.ReactNode;
  children: React.ReactNode;
  buttons?: React.ReactNode;
  plain?: boolean;
  searchValue?: string;
  onSearchValueChange?: (value: string) => void;
}) {
  return (
    <>
      <AppSidebar plain={plain} buttons={buttons} searchValue={searchValue} onSearchValueChange={onSearchValueChange}>
        {children}
      </AppSidebar>
      <SidebarInset>
        <header className="bg-background sticky top-0 flex shrink-0 items-center gap-2 border-b p-4">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 data-[orientation=vertical]:h-4" />
          <Breadcrumb>
            <BreadcrumbList>{breadcrumb}</BreadcrumbList>
          </Breadcrumb>
        </header>
        <div className="flex min-h-0 flex-1 flex-col gap-2 overflow-auto">{<Outlet />}</div>
      </SidebarInset>
    </>
  );
}
