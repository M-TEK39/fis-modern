import Link from "next/link";

function ReliefVehicleNotFound() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Record not found</p>
      <h2>The parent contract was not found.</h2>
      <Link className="button button-secondary" href="/contracts/maintenance">
        Back to Contracts
      </Link>
    </section>
  );
}

export { ReliefVehicleNotFound };
export default ReliefVehicleNotFound;
