import Link from "next/link";

export default function WorkshopHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="workshop-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Workshop</p>
            <h1 id="workshop-help-title">Workshop Maintenance Information / Help</h1>
          </div>
          <Link className="button button-secondary" href="/workshop">
            Back
          </Link>
        </header>
        <iframe
          src="/legacy/workshop/Doc_Workshop.htm"
          title="Workshop Help"
          className="help-iframe"
        />
      </section>
    </main>
  );
}
