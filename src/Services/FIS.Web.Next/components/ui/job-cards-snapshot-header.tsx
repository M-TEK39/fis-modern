import Link from "next/link";

export default function JobCardsSnapshotHeader() {
  return (
    <header className="vehicle-page-header">
      <div>
        <p className="eyebrow">Fleet maintenance</p>
        <h1 id="job-cards-title">Job Cards</h1>
        <p>Capture, authorize, close, cancel, print, and review vehicle job cards.</p>
      </div>
      <Link className="button button-secondary" href="/home">
        Home
      </Link>
    </header>
  );
}
