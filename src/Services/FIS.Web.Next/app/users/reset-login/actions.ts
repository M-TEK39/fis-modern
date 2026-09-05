"use server";

import { redirect } from "next/navigation";

import { resetUserLogin } from "@/lib/api-user-admin";
import { getSession } from "@/lib/session";

const USER_ADMIN_ROLE = "User Administration";
const RETURN_PATH = "/users/reset-login";

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

function resultPath(alphabet: string, username: string, result: string) {
  const params = new URLSearchParams({ alphabet, username, result });
  return `${RETURN_PATH}?${params.toString()}`;
}

export async function resetUserLoginAction(formData: FormData) {
  const session = await getSession();
  const username = getText(formData, "username");
  const alphabet = getAlphabet(getText(formData, "alphabet"));

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status !== "authenticated") {
    redirect(resultPath(alphabet, username, "unavailable"));
  }

  if (!hasUserAdministrationRole(session.roles)) {
    redirect(resultPath(alphabet, username, "forbidden"));
  }

  if (!username) {
    redirect(resultPath(alphabet, username, "missing-username"));
  }

  const result = await resetUserLogin(username);
  if (result.ok) {
    redirect(resultPath(alphabet, username, "success"));
  }

  redirect(resultPath(alphabet, username, result.reason));
}
