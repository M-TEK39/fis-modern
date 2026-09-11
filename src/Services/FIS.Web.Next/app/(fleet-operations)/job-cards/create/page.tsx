import Link from "next/link";

import { createJobCardAction } from "@/app/(fleet-operations)/job-cards/actions";
import { CreateJobCardView } from "@/app/(fleet-operations)/job-cards/_create-view";
import {
  AccessRestricted,
  JobCardPageBoundary,
  SessionProblem,
} from "@/app/(fleet-operations)/job-cards/_page";
import { getJobCardSession, queryValue } from "@/app/(fleet-operations)/job-cards/_page-utils";
import { hasJobCardAccess, hasRole } from "@/app/(fleet-operations)/job-cards/_utils";
import { getExtraCodes, ExtraCodeApiError } from "@/lib/api/reference-data/api-extra-codes";
import { getVehicleOptions, VehicleApiError } from "@/lib/api/vehicles/api-vehicles";

export default function CreateJobCardPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <CreateJobCardContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function CreateJobCardContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/create" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;

  const query = await searchParams;
  const vmfCode = Number(queryValue(query.vmfCode));
  const search = queryValue(query.search).trim();
  const saved = queryValue(query.saved) === "1";
  const errorMessage = queryValue(query.error);
  try {
    const [vehicles, extraCodes] = await Promise.all([getVehicleOptions(), getExtraCodes()]);
    const normalized = search.toLocaleLowerCase();
    const matchingVehicles = normalized
      ? vehicles.filter((vehicle) =>
          [vehicle.fleetNumber, vehicle.registrationNumber, vehicle.vmfCode].some((value) =>
            String(value ?? "")
              .toLocaleLowerCase()
              .includes(normalized),
          ),
        )
      : vehicles;
    const vehicle =
      Number.isInteger(vmfCode) && vmfCode > 0
        ? vehicles.find((item) => item.vmfCode === vmfCode)
        : null;
    return (
      <CreateJobCardView
        errorMessage={errorMessage}
        extraCodes={extraCodes}
        matchingVehicles={matchingVehicles}
        saved={saved}
        search={search}
        vehicle={vehicle ?? undefined}
      />
    );
  } catch (error) {
    const message =
      error instanceof VehicleApiError || error instanceof ExtraCodeApiError
        ? "Vehicle or job card categories could not be loaded."
        : "The create Job Card page could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/capturer-default">
            Back
          </Link>
        </section>
      </main>
    );
  }
}
