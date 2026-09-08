export default function Loading() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-busy="true" aria-live="polite">
        <p className="eyebrow">Trips</p>
        <h1>Loading Trip Authority</h1>
        <p className="muted-copy">Loading vehicle and trip authority data…</p>
      </section>
    </main>
  );
}
