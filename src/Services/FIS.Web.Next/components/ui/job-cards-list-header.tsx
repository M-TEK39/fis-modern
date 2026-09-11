import Link from "next/link";

export default function JobCardsListHeader() {
  return (
    <header className="vehicle-page-header">
      <div>
        <p className="eyebrow">Job Cards</p>
        <h1 id="list-job-cards-title">List / Edit Job Cards</h1>
        <p>Search by GG or GP number, select a job card, then review its details.</p>
      </div>
      <div className="button-row">
        <Link className="button button-primary" href="/job-cards/select-vehicle">
          Add new job card
        </Link>
        <Link className="button button-secondary" href="/job-cards/capturer-default">
          Main menu
        </Link>
      </div>
    </header>
  );
}
