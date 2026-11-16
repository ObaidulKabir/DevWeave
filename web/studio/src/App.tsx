import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthContext';
import { GuestOnly } from './components/GuestOnly';
import { RequireAuth } from './components/RequireAuth';
import { Shell } from './components/Shell';
import { AccessPage } from './pages/AccessPage';
import { AgentsPage } from './pages/AgentsPage';
import { CatalogPage } from './pages/CatalogPage';
import { ActivityPage } from './pages/ActivityPage';
import { CoordinationPage } from './pages/CoordinationPage';
import { DocumentsPage } from './pages/DocumentsPage';
import { EvidencePage } from './pages/EvidencePage';
import { KnowledgePage } from './pages/KnowledgePage';
import { LandingPage } from './pages/LandingPage';
import { LiveOperationsPage } from './pages/LiveOperationsPage';
import { LoginPage } from './pages/LoginPage';
import { OverviewPage } from './pages/OverviewPage';
import { PlanMapPage } from './pages/PlanMapPage';
import { PlanOutlinePage } from './pages/PlanOutlinePage';
import { PlanVersionsPage } from './pages/PlanVersionsPage';
import { RegisterPage } from './pages/RegisterPage';
import { RelationsPage } from './pages/RelationsPage';
import { ReportsPage } from './pages/ReportsPage';
import { SettingsPage } from './pages/SettingsPage';
import { SpecificationsPage } from './pages/SpecificationsPage';
import { TenantAppsPage } from './pages/TenantAppsPage';
import { WorkPage } from './pages/WorkPage';
import { WorkspaceProvider } from './workspace/WorkspaceContext';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<GuestOnly />}>
            <Route path="/" element={<LandingPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
          </Route>

          <Route element={<RequireAuth />}>
            <Route
              element={
                <WorkspaceProvider>
                  <Shell />
                </WorkspaceProvider>
              }
            >
              <Route path="apps" element={<TenantAppsPage />} />
              <Route path="overview" element={<OverviewPage />} />
              <Route path="plan-map" element={<PlanMapPage />} />
              <Route path="plan-outline" element={<PlanOutlinePage />} />
              <Route path="plan-versions" element={<PlanVersionsPage />} />
              <Route path="knowledge" element={<KnowledgePage />} />
              <Route path="documents" element={<DocumentsPage />} />
              <Route path="specifications" element={<SpecificationsPage />} />
              <Route path="catalog" element={<CatalogPage />} />
              <Route path="relations" element={<RelationsPage />} />
              <Route path="work" element={<WorkPage />} />
              <Route path="activity" element={<ActivityPage />} />
              <Route path="evidence" element={<EvidencePage />} />
              <Route path="access" element={<AccessPage />} />
              <Route path="agents" element={<AgentsPage />} />
              <Route path="live" element={<LiveOperationsPage />} />
              <Route path="coordination" element={<CoordinationPage />} />
              <Route path="reports" element={<ReportsPage />} />
              <Route path="settings" element={<SettingsPage />} />
              <Route path="*" element={<Navigate to="/apps" replace />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
