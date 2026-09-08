export default function Loading() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-busy="true">
        <p className="eyebrow">Trips</p>
        <h1>Loading trip authority…</h1>
        <p className="muted-copy">
          Reading the persisted trip, route, driver, and passenger records.
        </p>
      </section>
    </main>
  );
}
