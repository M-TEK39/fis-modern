"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasTripAuthorityAccess } from "@/app/trips/_page";
import { closeTripAuthority, TripAuthorityApiError } from "@/lib/api-trip-authorities";
import { getSession } from "@/lib/session";

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
          : "error";
  }

  return "error";
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

  if (routeCodes.some((routeCode) => routeCode === null) || new Set(routeCodes).size !== routeCodes.length) {
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
      routes: routes.filter((route): route is { routeCode: number; endOdometer: number } => route !== null),
    });
  } catch (error) {
    redirect(resultPath(tripId, apiResult(error)));
  }

  revalidatePath("/trip-authorities");
  revalidatePath("/trips");
  revalidatePath(RETURN_PATH);

  if (intent === "renew") {
    const params = new URLSearchParams({ mode: "Renew", contractCode: textValue(formData, "contractCode") });
    const vmfCode = positiveInteger(textValue(formData, "vmfCode"));
    if (vmfCode !== null) params.set("vmfCode", String(vmfCode));
    redirect(`/trips/create?${params.toString()}`);
  }

  redirect(resultPath(tripId, "closed"));
}
