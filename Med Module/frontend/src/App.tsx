import { HashRouter, Navigate, Route, Routes } from 'react-router-dom';
import { useEffect } from 'react';
import { RequireAuth } from './components/Auth/RequireAuth';
import { AppLayout } from './components/Layout/AppLayout';
import { AppStateProvider } from './context/AppStateContext';
import { AuthPage } from './pages/AuthPage';
import { ExtractionPage } from './pages/ExtractionPage';
import { HomePage } from './pages/HomePage';
import { RequestLogPage } from './pages/RequestLogPage';

export default function App() {
  useEffect(() => {
    function handleUnauthorized() {
      if (window.location.hash !== '#/auth') {
        window.location.hash = '#/auth';
      }
    }

    window.addEventListener('med-module:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('med-module:unauthorized', handleUnauthorized);
  }, []);

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
            <Route path="/auth" element={<AuthPage />} />
            <Route
              path="/extract"
              element={
                <RequireAuth>
                  <ExtractionPage />
                </RequireAuth>
              }
            />
            <Route
              path="/requests"
              element={
                <RequireAuth>
                  <RequestLogPage />
                </RequireAuth>
              }
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AppLayout>
      </AppStateProvider>
    </HashRouter>
  );
}
