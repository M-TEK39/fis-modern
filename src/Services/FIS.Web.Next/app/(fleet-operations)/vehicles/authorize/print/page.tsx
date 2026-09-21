import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import PrintButton from "@/app/(fleet-operations)/contracts/print-button";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import AccessRestrictedCard from "@/components/app-shell/access-restricted-card";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import AuthorizedPrintAuto from "@/app/(fleet-operations)/vehicles/authorize/authorized-print-auto";
import { canViewVehicleInception } from "@/app/(fleet-operations)/vehicles/access";
import {
  printVehicleAuthorization,
  VehicleAuthorizationApiError,
  type VehicleAuthorization,
} from "@/lib/api/vehicles/api-vehicle-authorization";
import { getSession } from "@/lib/auth/session";

type PrintSearchParams = Record<string, string | string[] | undefined>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function allowedReturnPath(value: string | undefined) {
  return value === "/vehicles/authorize" ? "/vehicles/authorize" : "/vehicles/create";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === ""
    ? "-"
    : String(value);
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 10);
  }

  return `${date.getUTCFullYear()}/${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")}`;
}

function formatDateTime(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const datePart = formatDate(value);
  const time = `${String(date.getUTCHours()).padStart(2, "0")}:${String(date.getUTCMinutes()).padStart(2, "0")}:${String(date.getUTCSeconds()).padStart(2, "0")}`;
  return `${datePart} ${time}`;
}

function PrintField({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function AccessRestricted() {
  return (
    <AccessRestrictedCard message="You do not have permission to print authorized vehicle inception details." />
  );
}

async function PrintAuthorizedVehiclePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<PrintSearchParams> }>) {
  await connection();
  const session = await getSession();
  const query = await searchParams;
  const vehicleId = positiveInt(getQueryValue(query.id));
  const returnPath = allowedReturnPath(getQueryValue(query.returnPath));

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={returnPath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Authorized vehicle printout could not be loaded.</h2>
          <Link className="button button-secondary" href={returnPath}>
            Back
          </Link>
        </section>
      </main>
    );
  }

  if (!canViewVehicleInception(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  if (!vehicleId) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Vehicle not selected</p>
          <h2>Choose an authorized vehicle from the print list.</h2>
          <Link className="button button-secondary" href={returnPath}>
            Back
          </Link>
        </section>
      </main>
    );
  }

  try {
    const vehicle = await printVehicleAuthorization(vehicleId);
    return <AuthorizedVehiclePrintout vehicle={vehicle} returnPath={returnPath} />;
  } catch (error) {
    if (error instanceof VehicleAuthorizationApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={returnPath} />
        </main>
      );
    }

    if (error instanceof VehicleAuthorizationApiError && error.reason === "forbidden") {
      return (
        <main className="page-shell vehicle-page-shell">
          <AccessRestricted />
        </main>
      );
    }

    console.error(
      "FIS authorized vehicle print failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Print unavailable</p>
          <h2>Authorized vehicle details could not be printed.</h2>
          <p className="muted-copy">
            {error instanceof VehicleAuthorizationApiError
              ? error.message
              : "The vehicle authorization service returned an unexpected response."}
          </p>
          <Link className="button button-secondary" href={returnPath}>
            Back
          </Link>
        </section>
      </main>
    );
  }
}

function AuthorizedVehiclePrintout({
  vehicle,
  returnPath,
}: Readonly<{ vehicle: VehicleAuthorization; returnPath: string }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <AuthorizedPrintAuto />
      <article
        className="vehicle-card contract-printout"
        aria-labelledby="authorized-vehicle-print-title"
      >
        <header className="vehicle-page-header vehicle-inception-print-actions">
          <div>
            <p className="eyebrow">g-FleeT Management</p>
            <h1 id="authorized-vehicle-print-title">Authorized Vehicle Master Information</h1>
            <p>{valueOrDash(vehicle.chassisNumber)}</p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href={returnPath}>
              Back
            </Link>
            <PrintButton label="Print this page" />
          </div>
        </header>
        <section className="vehicle-status-maintenance-panel">
          <dl className="contract-facts">
            <PrintField label="Current GG No :" value={valueOrDash(vehicle.fleetNumber)} />
            <PrintField
              label="Registration Number :"
              value={valueOrDash(vehicle.registrationNumber)}
            />
            <PrintField label="Replace GG No :" value={valueOrDash(vehicle.replacedGgNumber)} />
            <PrintField
              label="Status :"
              value={valueOrDash(vehicle.statusDescription ?? vehicle.authorityStatus)}
            />
            <PrintField label="GP Number :" value={valueOrDash(vehicle.gpNumber)} />
            <PrintField label="Invoice Number :" value={valueOrDash(vehicle.invoiceNumber)} />
            <PrintField label="Make & Model :" value={valueOrDash(vehicle.modelDescription)} />
            <PrintField
              label="Year Manufactured :"
              value={valueOrDash(vehicle.yearManufactured)}
            />
            <PrintField label="Colour :" value={valueOrDash(vehicle.colour)} />
            <PrintField
              label="Location :"
              value={valueOrDash(vehicle.locationDescription ?? vehicle.locationCode)}
            />
            <PrintField label="VIN(Chassis) No :" value={valueOrDash(vehicle.chassisNumber)} />
            <PrintField label="Engine No :" value={valueOrDash(vehicle.engineNumber)} />
            <PrintField
              label="Hired From :"
              value={valueOrDash(vehicle.hiredFromDescription ?? vehicle.vsCode)}
            />
            <PrintField
              label="Hire Type :"
              value={valueOrDash(vehicle.hireTypeDescription ?? vehicle.typeCode)}
            />
            <PrintField label="Take On Odo :" value={valueOrDash(vehicle.takeOnOdo)} />
            <PrintField label="Take On Date :" value={formatDate(vehicle.takeOnDate)} />
            <PrintField label="Purchase From :" value={valueOrDash(vehicle.purchaseFrom)} />
            <PrintField label="Purchase Date :" value={formatDate(vehicle.purchaseDate)} />
            <PrintField label="Purchase Amount :" value={valueOrDash(vehicle.purchaseAmount)} />
            <PrintField
              label="Site Allocation:"
              value={valueOrDash(vehicle.siteName ?? vehicle.siteCode)}
            />
            {vehicle.extras.length > 0 ? (
              <PrintField label="Vehicle Extras" value={vehicle.extras.join(", ")} />
            ) : null}
            <PrintField
              label="Vehicle Damages:"
              value={
                [vehicle.damageStatus, vehicle.damagesComment].filter(Boolean).join(" — ") || "-"
              }
            />
            <PrintField label="Fleet Notes:" value={valueOrDash(vehicle.fleetNotes)} />
            <PrintField
              label="Authorizer`s Comment :"
              value={valueOrDash(vehicle.authorizationComment)}
            />
          </dl>
          <p>
            <strong>Vehicle Details Captured By: </strong>
            <span className="vehicle-print-underline">
              {valueOrDash(vehicle.capturedByUserName ?? vehicle.createdByUserCode)}
            </span>
            {" On: "}
            <span className="vehicle-print-underline">
              {formatDateTime(vehicle.capturedDate ?? vehicle.dateCreated)}
            </span>
          </p>
          <p>
            <strong>Captured Details Authorized By: </strong>
            <span className="vehicle-print-underline">
              {valueOrDash(vehicle.authorizedByUserName ?? vehicle.authorizedByUserCode)}
            </span>
            {" On: "}
            <span className="vehicle-print-underline">
              {formatDateTime(vehicle.authorizationDate)}
            </span>
          </p>
        </section>
      </article>
    </main>
  );
}

export default function PrintAuthorizedVehiclePage(props: Readonly<{
  searchParams: Promise<PrintSearchParams>;
}>) {
  return (
    <StreamedRoute>
      <PrintAuthorizedVehiclePageContent {...props} />
    </StreamedRoute>
  );
}
