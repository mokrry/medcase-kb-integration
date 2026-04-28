export function Loading({ label = 'Загрузка...' }: { label?: string }) {
  return (
    <div className="state-card">
      <div className="spinner" />
      <p>{label}</p>
    </div>
  );
}
