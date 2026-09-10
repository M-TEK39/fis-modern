"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasTripAuthorityAccess } from "@/app/(fleet-operations)/trips/_page";
import {
  closeTripAuthority,
  createTripAuthority,
  TripAuthorityApiError,
  type CreateTripAuthorityRequest,
} from "@/lib/api/fleet-operations/api-trip-authorities";
import { getDriverManagementSiteDriver } from "@/lib/api/reference-data/api-driver-management";
import { getUserAdminUserChoices } from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";

const RETURN_PATH = "/trips/show";

function textValue(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function nonNegativeInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : null;
}

function resultPath(tripId: number | null, result: string) {
  const params = new URLSearchParams({ result });
  if (tripId !== null) params.set("tripId", String(tripId));
  return `${RETURN_PATH}?${params.toString()}`;
}

function apiResult(error: unknown) {
  if (error instanceof TripAuthorityApiError) {
    return error.reason === "unauthorized"
      ? "unauthorized"
      : error.reason === "unavailable"
        ? "unavailable"
        : error.reason === "not-found"
          ? "not-found"
          : error.reason === "rejected"
            ? "rejected"
            : "error";
  }

  return "error";
}

function values(formData: FormData, name: string) {
  return formData
    .getAll(name)
    .filter((value): value is string => typeof value === "string")
    .map((value) => value.trim());
}

function dateValue(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null;
}

function createResultPath(formData: FormData, result: string) {
  const params = new URLSearchParams({ result });
  for (const name of ["mode", "contractCode", "vmfCode"]) {
    const value = textValue(formData, name);
    if (value) params.set(name, value);
  }
  return `/trips/create?${params.toString()}`;
}

function parseUserAccessCode(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function selectedDriverRequest(
  driver: NonNullable<Awaited<ReturnType<typeof getDriverManagementSiteDriver>>>,
  isPrimary: boolean,
) {
  return {
    name: `${driver.driverFirstname ?? ""} ${driver.driverSurname ?? ""}`.trim(),
    identityNumber: driver.driverSAId,
    isPrimary,
    siteCode: driver.siteCode,
    licenceTypeCode: driver.driverLicenceTypeId,
    passportNumber: driver.driverPassportNumber,
    persalNumber: driver.driverPersonalNumber,
    contractNumber: driver.driverContractNumber,
    licenceNumber: driver.driverLicenceNumber,
    licenceIssueDate: driver.driverLicenceIssueDate,
    licenceLastVerifiedDate: driver.driverLicenceLastVerifiedDate,
    hasPdp: driver.driverHasPDP,
    pdpExpiryDate: driver.driverPDPExpiryDate,
    licenceExpiryDate: driver.driverLicenceExpiryDate,
    isActive: driver.driverActive,
  } satisfies CreateTripAuthorityRequest["drivers"][number];
}

export async function createTripAuthorityAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") redirect(createResultPath(formData, "unavailable"));
  if (!hasTripAuthorityAccess(session)) redirect(createResultPath(formData, "forbidden"));

  const contractCode = positiveInteger(textValue(formData, "contractCode"));
  if (contractCode === null) redirect(createResultPath(formData, "missing-contract"));

  const tripReason = textValue(formData, "tripReason");
  const tripTypeCode = positiveInteger(textValue(formData, "tripTypeCode"));
  const approverCode = positiveInteger(textValue(formData, "approverCode"));
  if (!tripReason || tripTypeCode === null || approverCode === null) {
    redirect(createResultPath(formData, "validation"));
  }

  let approver;
  try {
    const approvers = await getUserAdminUserChoices();
    approver = approvers.find((candidate) => candidate.userAccessCode === approverCode);
  } catch (error) {
    console.error(
      "FIS trip approver lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    redirect(createResultPath(formData, "unavailable"));
  }

  if (!approver || approver.userAccessCode === parseUserAccessCode(session.userAccessCode)) {
    redirect(createResultPath(formData, "invalid-approver"));
  }

  const driverCodes = values(formData, "driverCode").map(positiveInteger);
  const driverPrimary = values(formData, "driverPrimary");
  if (
    driverCodes.length === 0 ||
    driverCodes.some((code) => code === null) ||
    new Set(driverCodes).size !== driverCodes.length
  ) {
    redirect(createResultPath(formData, "missing-driver"));
  }

  const passengerNames = values(formData, "passengerName").filter(Boolean);
  const routeStarts = values(formData, "routeStartDate");
  const routeEnds = values(formData, "routeEndDate");
  const routeStartLocations = values(formData, "routeStartLocation");
  const routeEndLocations = values(formData, "routeEndLocation");
  const routeDistances = values(formData, "routeEstimatedDistance");
  const routeResponsibilities = values(formData, "routeResponsibilityCode");
  const routeObjectives = values(formData, "routeObjectiveCode");
  const routeProjects = values(formData, "routeProjectNumber");
  const routeFunds = values(formData, "routeFundCode");
  const routeCount = routeStarts.length;
  if (
    routeCount === 0 ||
    [
      routeEnds,
      routeStartLocations,
      routeEndLocations,
      routeDistances,
      routeResponsibilities,
      routeObjectives,
      routeProjects,
      routeFunds,
    ].some((items) => items.length !== routeCount)
  ) {
    redirect(createResultPath(formData, "missing-route"));
  }

  const routes: CreateTripAuthorityRequest["routes"] = [];
  for (let index = 0; index < routeCount; index += 1) {
    const startDate = dateValue(routeStarts[index] ?? "");
    const endDate = dateValue(routeEnds[index] ?? "");
    const estimatedDistanceText = routeDistances[index] ?? "";
    const estimatedDistance = estimatedDistanceText
      ? nonNegativeInteger(estimatedDistanceText)
      : null;
    if (
      !startDate ||
      !endDate ||
      startDate > endDate ||
      (estimatedDistanceText && estimatedDistance === null) ||
      !routeResponsibilities[index] ||
      !routeObjectives[index] ||
      !routeProjects[index] ||
      !routeFunds[index]
    ) {
      redirect(createResultPath(formData, "validation"));
    }

    routes.push({
      startDate,
      endDate,
      startLocation: routeStartLocations[index] || null,
      endLocation: routeEndLocations[index] || null,
      estimatedDistance,
      responsibilityCode: routeResponsibilities[index] ?? "",
      objectiveCode: routeObjectives[index] ?? "",
      projectNumber: routeProjects[index] ?? "",
      fundCode: routeFunds[index] ?? "",
    });
  }

  let selectedDrivers;
  try {
    selectedDrivers = await Promise.all(
      driverCodes.map((code) =>
        code === null ? Promise.resolve(null) : getDriverManagementSiteDriver(code),
      ),
    );
  } catch (error) {
    console.error(
      "FIS trip driver lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    redirect(createResultPath(formData, "unavailable"));
  }

  if (selectedDrivers.some((driver) => driver === null)) {
    redirect(createResultPath(formData, "invalid-driver"));
  }

  const selectedDriverRequests = selectedDrivers.map((driver, index) =>
    selectedDriverRequest(driver!, driverPrimary[index] === "true"),
  );
  const expiryDate = routes.reduce(
    (latest, route) => (route.endDate > latest ? route.endDate : latest),
    routes[0]?.endDate ?? "",
  );
  const request: CreateTripAuthorityRequest = {
    contractCode,
    approverName:
      approver.userName || `${approver.firstName ?? ""} ${approver.lastName ?? ""}`.trim(),
    approverRank: approver.positionName || "Approver",
    approverTelephone: approver.telephone,
    expiryDate,
    tripReason,
    tripRequestNumber: textValue(formData, "tripRequestNumber") || null,
    tripTypeCode,
    tripIncidentTypeCode: 1,
    userAccessCode: parseUserAccessCode(session.userAccessCode),
    tripIsMonthly: false,
    drivers: selectedDriverRequests,
    passengers: passengerNames.map((name) => ({ name })),
    routes,
  };

  try {
    const created = await createTripAuthority(request);
    revalidatePath("/trip-authorities");
    revalidatePath("/trips");
    redirect(`/trips/show?tripId=${created.tripId}&result=created`);
  } catch (error) {
    redirect(createResultPath(formData, apiResult(error)));
  }
}

export async function closeTripAuthorityAction(formData: FormData) {
  const tripId = positiveInteger(textValue(formData, "tripId"));
  const intent = textValue(formData, "intent");
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") redirect(resultPath(tripId, "unavailable"));
  if (!hasTripAuthorityAccess(session)) redirect(resultPath(tripId, "forbidden"));
  if (tripId === null) redirect(resultPath(null, "missing-trip"));

  const rawRouteCodes = formData.getAll("routeCode");
  const routeCodes = rawRouteCodes
    .filter((value): value is string => typeof value === "string")
    .map(positiveInteger);

  if (
    routeCodes.some((routeCode) => routeCode === null) ||
    new Set(routeCodes).size !== routeCodes.length
  ) {
    redirect(resultPath(tripId, "validation"));
  }

  const routes = routeCodes.map((routeCode) => {
    const endOdometer = nonNegativeInteger(textValue(formData, `routeEndOdometer-${routeCode}`));
    return routeCode === null || endOdometer === null ? null : { routeCode, endOdometer };
  });

  if (routes.some((route) => route === null)) redirect(resultPath(tripId, "validation"));

  const endOdometerText = textValue(formData, "endOdometer");
  const endOdometer = endOdometerText ? nonNegativeInteger(endOdometerText) : null;
  if (endOdometerText && endOdometer === null) redirect(resultPath(tripId, "validation"));

  try {
    await closeTripAuthority(tripId, {
      endOdometer,
      routes: routes.filter(
        (route): route is { routeCode: number; endOdometer: number } => route !== null,
      ),
    });
  } catch (error) {
    redirect(resultPath(tripId, apiResult(error)));
  }

  revalidatePath("/trip-authorities");
  revalidatePath("/trips");
  revalidatePath(RETURN_PATH);

  if (intent === "renew") {
    const params = new URLSearchParams({
      mode: "Renew",
      contractCode: textValue(formData, "contractCode"),
    });
    const vmfCode = positiveInteger(textValue(formData, "vmfCode"));
    if (vmfCode !== null) params.set("vmfCode", String(vmfCode));
    redirect(`/trips/create?${params.toString()}`);
  }

  redirect(resultPath(tripId, "closed"));
}
