"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createFuelCard,
  createPrivateHireFuelCard,
  deleteFuelCard,
  deletePrivateHireFuelCard,
  FuelCardApiError,
} from "@/lib/api-fuel-cards";
import { getSession } from "@/lib/session";

const FUEL_CARDS_ROLE = "Fuelcards";

class FuelCardValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getPositiveInt(formData: FormData, key: string, label: string, allowZero = false) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || (allowZero ? parsed < 0 : parsed <= 0)) {
    throw new FuelCardValidationError(
      `${label} must be a ${allowZero ? "zero or greater" : "positive whole number"}.`,
    );
  }
  return parsed;
}

function getReturnPath(formData: FormData, fallback: string) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : fallback;
}

function redirectWithMessage(path: string, key: string, message: string): never {
  redirect(`${path}?${new URLSearchParams({ [key]: message }).toString()}`);
}

async function authorizeFuelCards() {
  const session = await getSession();
  if (session.status === "unavailable")
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  if (session.status !== "authenticated")
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  if (
    !session.roles.some(
      (role) => role.localeCompare(FUEL_CARDS_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return { ok: false as const, message: "You do not have permission to maintain Fuelcards." };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, subject: string) {
  if (error instanceof FuelCardApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The fuel card service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found") return `The ${subject} record was not found.`;
  }
  return `${subject[0].toUpperCase()}${subject.slice(1)} operation failed. Please try again.`;
}

export async function saveFuelCardAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/fuel-cards/vehicle");
  const access = await authorizeFuelCards();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const vmfCode = getPositiveInt(formData, "vmfCode", "Vehicle");
    const counter = getPositiveInt(formData, "counter", "Counter", true);
    const now = new Date();
    await createFuelCard({
      vmf_code: vmfCode,
      Counter: counter,
      card_number: getText(formData, "cardNumber") || null,
      PAN_number: getText(formData, "panNumber") || null,
      ExpReason: "In Service",
      Status_date: now.toISOString(),
      PetTaken: now.toISOString(),
      PetExpire: new Date(now.getFullYear() + 1, now.getMonth(), now.getDate()).toISOString(),
      LinkGGNum: getText(formData, "ggNumber") || null,
    });
    revalidatePath("/fuel-cards");
    revalidatePath("/fuel-cards/vehicle");
    redirectWithMessage(returnPath, "saved", "Fuelcard added successfully.");
  } catch (error) {
    if (error instanceof FuelCardValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "fuelcard"));
  }
}

export async function savePrivateHireFuelCardAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/fuel-cards/private-hire/vehicle");
  const access = await authorizeFuelCards();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const counter = getPositiveInt(formData, "counter", "Counter", true);
    const registrationNumber = getText(formData, "registrationNumber");
    if (!registrationNumber)
      throw new FuelCardValidationError("A registration number is required.");
    await createPrivateHireFuelCard({
      RegistrationNumber: registrationNumber,
      Counter: counter,
      CardNumber: getText(formData, "cardNumber") || null,
      PanNumber: getText(formData, "panNumber") || null,
    });
    revalidatePath("/fuel-cards");
    revalidatePath("/fuel-cards/private-hire/vehicle");
    redirectWithMessage(returnPath, "saved", "Private hire fuelcard added successfully.");
  } catch (error) {
    if (error instanceof FuelCardValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "private hire fuelcard"));
  }
}

export async function deleteFuelCardAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/fuel-cards/delete");
  const access = await authorizeFuelCards();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);
  try {
    const code = getPositiveInt(formData, "fuelCardCode", "Fuelcard");
    await deleteFuelCard(code);
    revalidatePath("/fuel-cards");
    revalidatePath("/fuel-cards/delete");
    redirectWithMessage(returnPath, "deleted", "Fuelcard deleted successfully.");
  } catch (error) {
    if (error instanceof FuelCardValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "fuelcard"));
  }
}

export async function deletePrivateHireFuelCardAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/fuel-cards/private-hire/delete");
  const access = await authorizeFuelCards();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);
  try {
    const code = getPositiveInt(formData, "fuelCardCode", "Fuelcard");
    await deletePrivateHireFuelCard(code);
    revalidatePath("/fuel-cards");
    revalidatePath("/fuel-cards/private-hire/delete");
    redirectWithMessage(returnPath, "deleted", "Private hire fuelcard deleted successfully.");
  } catch (error) {
    if (error instanceof FuelCardValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "private hire fuelcard"));
  }
}
