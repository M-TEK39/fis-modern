export default function Loading() {
  return (
    <main className="page-shell" aria-busy="true">
      <div className="loading-card">
        <span className="spinner" aria-hidden="true" />
        <p>Loading Fleet Information System...</p>
      </div>
    </main>
  );
}
