import Link from "next/link";

export default function LossesHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Losses</p>
            <h1 id="loss-help-title">Losses Maintenance Help</h1>
            <p>
              Use the vehicle loss maintenance workflow to search by GG or GP number, review
              history, and maintain the loss record.
            </p>
          </div>
          <Link className="button button-secondary" href="/Losses/MNTLosses.aspx">
            Losses Menu
          </Link>
        </header>
        <section className="vehicle-status-maintenance-panel">
          <h2>Legacy workflow</h2>
          <ol>
            <li>Open Vehicle Losses Maintenance from the Losses menu.</li>
            <li>Search by GG number or GP registration number.</li>
            <li>
              Choose Edit or Delete for an existing loss, or Add Losses to capture a new record.
            </li>
          </ol>
          <p className="muted-copy">
            The workflow keeps the original losses table fields and works with both client-era and
            expanded databases.
          </p>
        </section>
      </section>
    </main>
  );
}
