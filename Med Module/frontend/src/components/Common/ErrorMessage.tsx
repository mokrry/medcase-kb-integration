export function ErrorMessage({ message }: { message: string }) {
  return (
    <div className="state-card state-card--error">
      <h3>Ошибка</h3>
      <p>{message}</p>
    </div>
  );
}
