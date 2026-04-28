import type { PropsWithChildren } from 'react';
import { Header } from './Header';

export function AppLayout({ children }: PropsWithChildren) {
  return (
    <div className="app-shell">
      <Header />
      <main className="container main-content">{children}</main>
    </div>
  );
}
