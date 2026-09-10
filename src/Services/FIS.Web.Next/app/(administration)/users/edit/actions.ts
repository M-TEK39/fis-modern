"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { userAdminAccessOptions } from "@/app/(administration)/users/user-admin-access";
import {
  updateUserAdminProfile,
  type UserAdminProfileInput,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";

const USER_ADMIN_ROLE = "User Administration";
const RETURN_PATH = "/users/edit";
const INT32_MIN = -2_147_483_648;
const INT32_MAX = 2_147_483_647;

class UserFormValidationError extends Error {}

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredText(formData: FormData, name: string, label: string, maxLength: number) {
  const value = getText(formData, name);
  if (!value) {
    throw new UserFormValidationError(`${label} is required.`);
  }

  if (value.length > maxLength) {
    throw new UserFormValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value;
}

function getOptionalInteger(formData: FormData, name: string, label: string) {
  const value = getText(formData, name);
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < INT32_MIN || parsed > INT32_MAX) {
    throw new UserFormValidationError(`${label} must be a valid whole number.`);
  }

  return parsed;
}

function getRequiredInteger(formData: FormData, name: string, label: string) {
  const value = getOptionalInteger(formData, name, label);
  if (value === null) {
    throw new UserFormValidationError(`${label} is required.`);
  }

  return value;
}

function getAccessLevel(formData: FormData) {
  const allowedBits = new Set<number>(userAdminAccessOptions.map((option) => option.permissionBit));
  let accessLevel = 0;

  for (const value of formData.getAll("accessLevel")) {
    if (typeof value !== "string" || !/^\d+$/.test(value)) {
      throw new UserFormValidationError("Access level contains an invalid permission.");
    }

    const permissionBit = Number(value);
    if (!allowedBits.has(permissionBit)) {
      throw new UserFormValidationError("Access level contains an invalid permission.");
    }

    accessLevel |= permissionBit;
  }

  return accessLevel;
}

function getSiteCode(formData: FormData) {
  const siteCode = getRequiredInteger(formData, "siteCode", "Site");
  if (siteCode <= 0 || siteCode > 32_767) {
    throw new UserFormValidationError("Site must be a valid site code.");
  }

  return siteCode;
}

function getPositionCode(formData: FormData) {
  const positionCode = getRequiredInteger(formData, "positionCode", "Position");
  if (positionCode <= 0 || positionCode > 255) {
    throw new UserFormValidationError("Position must be a valid position code.");
  }

  return positionCode;
}

function getAlphabet(value: string) {
  const candidate = value.trim().toUpperCase();
  return /^[A-Z]$/.test(candidate) ? candidate : "A";
}

function resultPath(username: string, alphabet: string, result: string) {
  return `${RETURN_PATH}?${new URLSearchParams({ Username: username, Alphabet: alphabet, result }).toString()}`;
}

function buildUpdateRequest(formData: FormData): {
  userAccessCode: number;
  username: string;
  alphabet: string;
  input: Omit<UserAdminProfileInput, "userName">;
} {
  const username = getRequiredText(formData, "username", "Username", 255);
  const alphabet = getAlphabet(getText(formData, "alphabet"));
  const userAccessCode = getRequiredInteger(formData, "userAccessCode", "User access code");
  if (userAccessCode <= 0 || userAccessCode > 32_767) {
    throw new UserFormValidationError("The selected user access code is invalid.");
  }

  const firstName = getRequiredText(formData, "firstName", "First Name", 255);
  const lastName = getRequiredText(formData, "lastName", "Last Name", 255);
  const email = getRequiredText(formData, "email", "E-mail", 255);
  if (!/^([^\s@]+)@([^\s@]+)\.([^\s@]+)$/.test(email)) {
    throw new UserFormValidationError("E-mail must be valid.");
  }

  const telephone = getText(formData, "telephone") || null;
  const cellphoneNumber = getOptionalInteger(formData, "cellphoneNumber", "Cell");
  if (!telephone && cellphoneNumber === null) {
    throw new UserFormValidationError("Cell or Tel Number is required.");
  }

  const persalNumber = getOptionalInteger(formData, "persalNumber", "Persal");
  const contractNumber = getOptionalInteger(formData, "contractNumber", "Contract Number");
  if (persalNumber === null && contractNumber === null) {
    throw new UserFormValidationError("Persal or Contract Number is required.");
  }

  const saIdNumber = getRequiredInteger(formData, "saIdNumber", "ID");
  if (saIdNumber <= 0) {
    throw new UserFormValidationError("ID must be a valid whole number.");
  }

  const approverCodeAtGfleet = getRequiredInteger(
    formData,
    "approverCodeAtGfleet",
    "Client Approver Name",
  );
  if (approverCodeAtGfleet <= 0) {
    throw new UserFormValidationError("Client Approver Name is required.");
  }

  return {
    userAccessCode,
    username,
    alphabet,
    input: {
      firstName,
      lastName,
      email,
      telephone,
      siteCode: getSiteCode(formData),
      positionCode: getPositionCode(formData),
      persalNumber,
      contractNumber,
      saIdNumber,
      passportNumber: getOptionalInteger(formData, "passportNumber", "Passport Number"),
      cellphoneNumber,
      faxNumber: getOptionalInteger(formData, "faxNumber", "Fax"),
      approverCodeAtGfleet,
      accessLevel: getAccessLevel(formData),
    },
  };
}

export async function updateUserAdminAction(formData: FormData) {
  let request: ReturnType<typeof buildUpdateRequest>;
  try {
    request = buildUpdateRequest(formData);
  } catch (error) {
    const username = getText(formData, "username");
    const alphabet = getAlphabet(getText(formData, "alphabet"));
    if (error instanceof UserFormValidationError) {
      redirect(resultPath(username, alphabet, "invalid"));
    }

    throw error;
  }

  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status !== "authenticated") {
    redirect(
      resultPath(
        request.username,
        request.alphabet,
        session.status === "expired" ? "unauthorized" : "unavailable",
      ),
    );
  }

  if (!hasUserAdministrationRole(session.roles)) {
    redirect(resultPath(request.username, request.alphabet, "forbidden"));
  }

  const result = await updateUserAdminProfile(request.userAccessCode, request.input);
  if (!result.ok) {
    redirect(resultPath(request.username, request.alphabet, result.reason));
  }

  revalidatePath("/users");
  revalidatePath("/UserAdmin/UserAdmin.aspx");
  redirect(resultPath(request.username, request.alphabet, "success"));
}
