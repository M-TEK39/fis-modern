import DataTableHeader from "@/components/ui/data-table-header";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { connection } from "next/server";

import CreateTripForm from "@/app/(fleet-operations)/trips/create/create-trip-form";
import { createTripAuthorityAction } from "@/app/(fleet-operations)/trips/actions";
import {
  getTripSession,
  hasTripAuthorityAccess,
  parsePositiveInteger,
  queryValue,
  tripAccessRestricted,
  tripSessionMessage,
} from "@/app/(fleet-operations)/trips/_page";
import {
  getDriverManagementSiteDrivers,
  type DriverManagementDriver,
} from "@/lib/api/reference-data/api-driver-management";
import {
  ContractApiError,
  getContract,
  type ContractRecord,
} from "@/lib/api/finance/api-contracts";
import {
  getTripAuthorityVehicles,
  type TripAuthorityVehicle,
} from "@/lib/api/fleet-operations/api-trip-authorities";
import {
  getUserAdminUserChoices,
  type UserAdminProfile,
} from "@/lib/api/administration/api-user-admin";
import {
  getVehicleForStatus,
  type VehicleStatusVehicle,
} from "@/lib/api/vehicles/api-vehicle-status";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getNumber(query: Record<string, string | string[] | undefined>, ...keys: string[]) {
  for (const key of keys) {
    const value = parsePositiveInteger(queryValue(query[key]));
    if (value !== null) return value;
  }

  return null;
}

function resultMessage(result: string) {
  switch (result) {
    case "missing-contract":
      return "Select a current vehicle contract before creating a trip authority.";
    case "forbidden":
      return "Your profile does not include Trip Authority access.";
    case "unavailable":
      return "The trip authority service is unavailable. Retry when the API is available.";
    case "created":
      return "Trip authority created successfully.";
    default:
      return null;
  }
}

function isContractManager(user: UserAdminProfile) {
  try {
    return (BigInt(String(user.accessLevel)) & BigInt(2)) === BigInt(2);
  } catch {
    return false;
  }
}

function filterApprovers(users: UserAdminProfile[], currentUserCode: number | null) {
  const candidates = users.filter(
    (user) => user.userAccessCode !== currentUserCode && user.userActive,
  );
  const contractManagers = candidates.filter(isContractManager);
  return (contractManagers.length > 0 ? contractManagers : candidates).sort((left, right) =>
    (left.userName ?? `${left.firstName ?? ""} ${left.lastName ?? ""}`).localeCompare(
      right.userName ?? `${right.firstName ?? ""} ${right.lastName ?? ""}`,
    ),
  );
}

function vehicleLabel(vehicle: TripAuthorityVehicle) {
  return `${vehicle.fleetNumber ?? "-"} / ${vehicle.registrationNumber ?? "-"}`;
}

function pageShell(children: React.ReactNode) {
  return <main className="page-shell vehicle-page-shell">{children}</main>;
}

function VehiclePicker({ vehicles }: Readonly<{ vehicles: TripAuthorityVehicle[] }>) {
  return pageShell(
    <section className="vehicle-status-card" aria-labelledby="trip-create-select-title">
      <p className="eyebrow">Trip Authority</p>
      <h1 id="trip-create-select-title">Select a current vehicle contract</h1>
      <p className="muted-copy">
        Choose the vehicle context before entering the trip authority details.
      </p>
      {vehicles.length === 0 ? (
        <p className="muted-copy">No current vehicle contracts are available.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Current vehicle contracts</caption>
            <DataTableHeader
              columns={[
                { key: "column-1", label: <>Fleet / registration</> },
                { key: "column-2", label: <>VMF</> },
                { key: "column-3", label: <>Contract</> },
                { key: "column-4", label: <>Site</> },
                { key: "column-5", label: <>Action</> },
              ]}
            />
            <tbody>
              {vehicles.map((vehicle) => (
                <tr key={`${vehicle.contractCode}-${vehicle.vmfCode}`}>
                  <td>{vehicleLabel(vehicle)}</td>
                  <td>{vehicle.vmfCode}</td>
                  <td>{vehicle.contractCode}</td>
                  <td>{vehicle.siteCode}</td>
                  <td>
                    <Link
                      className="button button-primary"
                      href={`/trips/create?vmfCode=${vehicle.vmfCode}&contractCode=${vehicle.contractCode}`}
                    >
                      Select
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>,
  );
}

function unavailablePage(message: string) {
  return pageShell(
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h1>Trip authority creation is unavailable.</h1>
      <p className="muted-copy">{message}</p>
      <Link className="button button-secondary" href="/trip-authorities">
        Back to Trip Authorities
      </Link>
    </section>,
  );
}

const CreateTripPageContent = renderCreateTripPageContent;

async function renderCreateTripPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getTripSession();
  const sessionMessage = tripSessionMessage(session, "/trips/create");
  if (sessionMessage) return sessionMessage;
  if (session.status !== "authenticated")
    return unavailablePage("Retry when the FIS API is available.");
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();

  const query = await searchParams;
  const vmfCode = getNumber(query, "vmfCode", "VMF", "Vehicle");
  const requestedContractCode = getNumber(query, "contractCode", "ContractCode");
  const mode = queryValue(query.mode || query.Mode) === "Renew" ? "Renew" : "Create";
  const result = queryValue(query.result);
  const currentUserCode = parsePositiveInteger(session.userAccessCode ?? "");

  if (vmfCode === null && requestedContractCode === null) {
    try {
      return <VehiclePicker vehicles={await getTripAuthorityVehicles()} />;
    } catch (error) {
      console.error(
        "FIS trip vehicle selection failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return unavailablePage("Current vehicle contracts could not be loaded.");
    }
  }

  let contract: ContractRecord | null = null;
  try {
    if (requestedContractCode !== null) {
      contract = await getContract(requestedContractCode);
    } else if (vmfCode !== null) {
      const vehicleContracts = await getTripAuthorityVehicles();
      const selected = vehicleContracts.find((vehicle) => vehicle.vmfCode === vmfCode);
      if (selected) contract = await getContract(selected.contractCode);
    }
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "not-found") {
      return unavailablePage("The selected vehicle contract no longer exists.");
    }
    console.error(
      "FIS trip contract context failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return unavailablePage("The selected vehicle contract could not be loaded.");
  }

  const selectedVmfCode = vmfCode ?? contract?.vmfCode ?? null;
  const selectedContractCode = requestedContractCode ?? contract?.contractCode ?? null;
  if (selectedVmfCode === null || selectedContractCode === null || contract === null) {
    return unavailablePage("The selected vehicle contract could not be resolved.");
  }

  let vehicle: VehicleStatusVehicle;
  let users: UserAdminProfile[];
  let drivers: DriverManagementDriver[];
  try {
    const [vehicleResult, userResult, driverResult] = await Promise.all([
      getVehicleForStatus(selectedVmfCode),
      getUserAdminUserChoices(),
      getDriverManagementSiteDrivers(contract.siteCode),
    ]);
    vehicle = vehicleResult;
    users = userResult;
    drivers = driverResult;
  } catch (error) {
    console.error(
      "FIS trip creation context failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return unavailablePage("Vehicle, approver, or driver information could not be loaded.");
  }

  const approvers = filterApprovers(users, currentUserCode);
  if (approvers.length === 0 || drivers.length === 0) {
    return pageShell(
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Trip Authority</p>
        <h1>Trip authority cannot be created yet.</h1>
        <p className="muted-copy">
          An active approver and at least one active site driver are required for this vehicle.
        </p>
        <Link className="button button-secondary" href="/trip-authorities">
          Back to Trip Authorities
        </Link>
      </section>,
    );
  }

  const message = resultMessage(result);
  const today = new Date().toISOString().slice(0, 10);
  return pageShell(
    <article className="vehicle-card" aria-labelledby="trip-create-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">{mode === "Renew" ? "Renewal" : "New request"}</p>
          <h1 id="trip-create-title">Trip Authority</h1>
          {message ? (
            <p
              className={result === "created" ? "notice notice-success" : "notice notice-error"}
              role={result === "created" ? "status" : "alert"}
            >
              {message}
            </p>
          ) : null}
        </div>
        <Link className="button button-secondary" href="/trip-authorities">
          Back to Trips
        </Link>
      </header>
      <CreateTripForm
        action={createTripAuthorityAction}
        context={{
          vmfCode: selectedVmfCode,
          contractCode: selectedContractCode,
          siteCode: contract.siteCode,
          fleetNumber: vehicle.fleetNumber ?? contract.fleetNumber,
          registrationNumber: vehicle.registrationNumber ?? contract.registrationNumber,
          modelName: vehicle.modelName,
          currentOdo: vehicle.currentOdo ?? contract.startOdometer,
        }}
        approvers={approvers}
        drivers={drivers}
        mode={mode}
        result={result}
        today={today}
      />
    </article>,
  );
}

export default function CreateTripPage(props: Parameters<typeof CreateTripPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <CreateTripPageContent {...props} />
    </Suspense>
  );
}
