import Link from "next/link";

export default function AssetVerificationHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="asset-verification-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle asset verification</p>
            <h1 id="asset-verification-help-title">
              Asset Verification Maintenance Information / Help
            </h1>
          </div>
          <Link className="button button-secondary" href="/vehicle-verification">
            Menu
          </Link>
        </header>
        <div className="vehicle-status-maintenance-panel">
          <p className="muted-copy">
            The original Asset Verification manual remains available as the read-only reference for
            this workflow.
          </p>
          <iframe
            src="/legacy/asset-verification.html"
            title="Asset Verification Help"
            className="fis-help-frame"
          />
        </div>
      </section>
    </main>
  );
}
