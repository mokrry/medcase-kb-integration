import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './components/Layout/AppLayout';
import { AppStateProvider } from './context/AppStateContext';
import { ExtractionPage } from './pages/ExtractionPage';
import { HomePage } from './pages/HomePage';
import { ReliabilityPage } from './pages/ReliabilityPage';

export default function App() {
  return (
    <HashRouter
      future={{
        v7_startTransition: true,
        v7_relativeSplatPath: true
      }}
    >
      <AppStateProvider>
        <AppLayout>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/extract" element={<ExtractionPage />} />
            <Route path="/reliability" element={<ReliabilityPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AppLayout>
      </AppStateProvider>
    </HashRouter>
  );
}
