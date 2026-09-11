import Link from "next/link";

export default function MonitorHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="monitor-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre Monitoring</p>
            <h1 id="monitor-help-title">Monitor Inquiry Information / Help</h1>
            <p>Reference information for the Monitor inquiry workflow.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <div className="module-help-content">
          <section className="module-help-section" aria-labelledby="monitor-help-workflow">
            <h2 id="monitor-help-workflow">Monitor inquiry workflow</h2>
            <p className="module-help-intro">
              Capture an inquiry against the selected vehicle, keep the driver and site details
              current, and use Reports to review captured inquiries.
            </p>
            <p>
              The original Call Centre role and legacy Monitor fields remain in force. If the API is
              temporarily unavailable, retry after the service has recovered.
            </p>
            <p className="module-help-note">
              The legacy Monitor Information / Help entry points to the under-construction page. No
              legacy Monitor manual content is available to reproduce here.
            </p>
            <div className="button-row">
              <Link className="button button-secondary" href="/monitor">
                Monitor menu
              </Link>
            </div>
          </section>
        </div>
      </article>
    </main>
  );
}
