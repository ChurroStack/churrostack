import { createRoot } from 'react-dom/client';
import { BrowserRouter, Outlet } from 'react-router';
import { OidcInitializationGate } from '@/oidc';
import { Navigate, Route, Routes } from 'react-router';
import { AutoLogoutWarningOverlay } from './components/logout-warning';
import './index.css';
import Home from './pages/home/home';
import EnvironmentIndex from './pages/environments';
import Environments from './pages/environments/environments';
import Environment from './pages/environments/environment';
import TemplateIndex from './pages/templates-admin';
import Templates from './pages/templates-admin/templates';
import Template from './pages/templates-admin/template';
import ApplicationIndex from './pages/applications';
import Applications from './pages/applications/applications';
import Application from './pages/applications/application';
import { EnvironmentServiceProvider } from './services/environment-services';
import { TemplateServiceProvider } from './services/template-services';
import { SidebarProvider } from './components/ui/sidebar';
import { ThemeProvider } from 'next-themes';
import { ApplicationServiceProvider } from './services/application-services';
import { NotificationProvider } from './services/notification-service';
import { Toaster } from './components/ui/sonner';
import { ProfileProvider } from './hooks/data/profile';
import IdentitiesPage from './pages/settings/identitites';
import SettingsIndex from './pages/settings/index';
import LlmIndex from './pages/llms';
import Llms from './pages/llms/llms';
import Llm from './pages/llms/llm';
import ApiKeyIndex from './pages/keys';
import ApiKeys from './pages/keys/keys';
import ApiKey from './pages/keys/key';
import { LlmServiceProvider } from './services/llm-services';
import { ApiKeyServiceProvider } from './services/api-key-services';
import GeneralPage from './pages/settings/general';
import Forbidden from './pages/forbidden';

function ApiKeyLayout() {
  return (
    <ApiKeyServiceProvider>
      <div className="absolute top-0 left-0 w-full bg-border h-px z-100"></div>
      <Outlet />
    </ApiKeyServiceProvider>
  );
}

function LlmLayout() {
  return (
    <LlmServiceProvider>
      <div className="absolute top-0 left-0 w-full bg-border h-px z-100"></div>
      <Outlet />
    </LlmServiceProvider>
  );
}

function TemplateLayout() {
  return (
    <TemplateServiceProvider>
      <div className="absolute top-0 left-0 w-full bg-border h-px z-100"></div>
      <Outlet />
    </TemplateServiceProvider>
  );
}

function EnvironmentLayout() {
  return (
    <EnvironmentServiceProvider>
      <div className="absolute top-0 left-0 w-full bg-border h-px z-100"></div>
      <Outlet />
    </EnvironmentServiceProvider>
  );
}

function ApplicationLayout() {
  return (
    <ApplicationServiceProvider>
      <div className="absolute top-0 left-0 w-full bg-border h-px z-100"></div>
      <Outlet />
    </ApplicationServiceProvider>
  );
}

createRoot(document.getElementById('root')!).render(
  // <StrictMode>
  <OidcInitializationGate>
    <NotificationProvider>
      <BrowserRouter>
        <ProfileProvider>
          <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
          <SidebarProvider
            style={
              {
                '--sidebar-width': '350px'
              } as React.CSSProperties
            }>
            <Routes>
              <Route index element={<Home />} />
              <Route path="/account/accessdenied" element={<Forbidden />} />
              <Route element={<EnvironmentLayout />}>
                <Route path="/environments" element={<EnvironmentIndex />}>
                  <Route index element={<Environments />} />
                  <Route path=":id" element={<Environment />} />
                </Route>
              </Route>
              <Route element={<ApplicationLayout />}>
                <Route path="/applications" element={<ApplicationIndex />}>
                  <Route index element={<Applications />} />
                  <Route path=":id" element={<Application />} />
                </Route>
              </Route>
              <Route element={<LlmLayout />}>
                <Route path="/llms" element={<LlmIndex />}>
                  <Route index element={<Llms />} />
                  <Route path=":id" element={<Llm />} />
                </Route>
              </Route>
              <Route element={<ApiKeyLayout />}>
                <Route path="/keys" element={<ApiKeyIndex />}>
                  <Route index element={<ApiKeys />} />
                  <Route path=":id" element={<ApiKey />} />
                </Route>
              </Route>
              <Route element={<TemplateLayout />}>
                <Route path="/templates" element={<TemplateIndex />}>
                  <Route index element={<Templates />} />
                  <Route path=":id" element={<Template />} />
                </Route>
              </Route>
              <Route path="/settings" element={<SettingsIndex />}>
                <Route index element={<GeneralPage />} />
                <Route path="general" element={<GeneralPage />} />
                <Route path="identities" element={<IdentitiesPage />} />
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
            <Toaster />
          </SidebarProvider>
          </ThemeProvider>
          <AutoLogoutWarningOverlay />
        </ProfileProvider>
      </BrowserRouter>
    </NotificationProvider>
  </OidcInitializationGate>
  // </StrictMode>
);
