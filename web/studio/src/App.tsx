import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Shell } from './components/Shell';
import { AccessPage } from './pages/AccessPage';
import { ActivityPage } from './pages/ActivityPage';
import { CoordinationPage } from './pages/CoordinationPage';
import { DocumentsPage } from './pages/DocumentsPage';
import { EvidencePage } from './pages/EvidencePage';
import { KnowledgePage } from './pages/KnowledgePage';
import { OverviewPage } from './pages/OverviewPage';
import { PlanMapPage } from './pages/PlanMapPage';
import { PlanOutlinePage } from './pages/PlanOutlinePage';
import { RelationsPage } from './pages/RelationsPage';
import { ReportsPage } from './pages/ReportsPage';
import { SettingsPage } from './pages/SettingsPage';
import { WorkPage } from './pages/WorkPage';
import { WorkspaceProvider } from './workspace/WorkspaceContext';

export default function App() {
  return (
    <WorkspaceProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Shell />}>
            <Route index element={<OverviewPage />} />
            <Route path="plan-map" element={<PlanMapPage />} />
          <Route path="plan-outline" element={<PlanOutlinePage />} />
            <Route path="knowledge" element={<KnowledgePage />} />
            <Route path="documents" element={<DocumentsPage />} />
            <Route path="relations" element={<RelationsPage />} />
            <Route path="work" element={<WorkPage />} />
            <Route path="activity" element={<ActivityPage />} />
            <Route path="evidence" element={<EvidencePage />} />
            <Route path="access" element={<AccessPage />} />
            <Route path="coordination" element={<CoordinationPage />} />
            <Route path="reports" element={<ReportsPage />} />
            <Route path="settings" element={<SettingsPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </WorkspaceProvider>
  );
}
