"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasTripAuthorityAccess } from "@/app/(fleet-operations)/trips/_page";
import {
  removeTripsWithoutRoutes,
  TripToolsApiError,
} from "@/lib/api/fleet-operations/api-trip-tools";
import { getSession } from "@/lib/auth/session";

const routePath = "/trips/remove-without-routes";

export async function removeTripsWithoutRoutesAction() {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") redirect(`${routePath}?result=unauthorized`);
  if (session.status !== "authenticated") redirect(`${routePath}?result=unavailable`);
  if (!hasTripAuthorityAccess(session)) redirect(`${routePath}?result=forbidden`);

  try {
    const removed = await removeTripsWithoutRoutes();
    revalidatePath(routePath);
    redirect(`${routePath}?result=success&removed=${removed}`);
  } catch (error) {
    const result =
      error instanceof TripToolsApiError && error.reason === "unauthorized"
        ? "unauthorized"
        : error instanceof TripToolsApiError && error.reason === "unavailable"
          ? "unavailable"
          : "error";
    redirect(`${routePath}?result=${result}`);
  }
}
