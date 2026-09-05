"use server";

import { redirect } from "next/navigation";

import {
  activateUser,
  deactivateExpiredPassword,
  deactivateUser,
  type UserAdminMutationResult,
} from "@/lib/api-user-admin";
import { getSession } from "@/lib/session";

const USER_ADMIN_ROLE = "User Administration";
const RETURN_PATH = "/users/deactivate";
type UserStatusOperation = "deactivate" | "deactivate-expired" | "activate";

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function getAlphabet(value: string) {
  const candidate = value.trim().toUpperCase();
  return /^[A-Z]$/.test(candidate) ? candidate : "A";
}

function getOperation(value: string): UserStatusOperation | null {
  return value === "deactivate" || value === "deactivate-expired" || value === "activate" ? value : null;
}

function resultPath(alphabet: string, username: string, operation: UserStatusOperation | "deactivate", result: string) {
  const params = new URLSearchParams({ alphabet, username, operation, result });
  return `${RETURN_PATH}?${params.toString()}`;
}

export async function updateUserStatusAction(formData: FormData) {
  const session = await getSession();
  const username = getText(formData, "username");
  const alphabet = getAlphabet(getText(formData, "alphabet"));
  const operation = getOperation(getText(formData, "operation"));

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status !== "authenticated") {
    redirect(resultPath(alphabet, username, "deactivate", "unavailable"));
  }

  if (!hasUserAdministrationRole(session.roles)) {
    redirect(resultPath(alphabet, username, operation ?? "deactivate", "forbidden"));
  }

  if (!username) {
    redirect(resultPath(alphabet, username, operation ?? "deactivate", "missing-username"));
  }

  if (!operation) {
    redirect(resultPath(alphabet, username, "deactivate", "invalid-operation"));
  }

  let result: UserAdminMutationResult;
  switch (operation) {
    case "activate":
      result = await activateUser(username);
      break;
    case "deactivate-expired":
      result = await deactivateExpiredPassword(username);
      break;
    default:
      result = await deactivateUser(username);
      break;
  }

  redirect(resultPath(alphabet, username, operation, result.ok ? "success" : result.reason));
}
