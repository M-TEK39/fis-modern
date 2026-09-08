"use server";

import { redirect } from "next/navigation";

import { forceUserPassword } from "@/lib/api-user-admin";
import { getSession } from "@/lib/session";

const USER_ADMIN_ROLE = "User Administration";
const RETURN_PATH = "/users/force-password-change";

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
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

export async function forceUserPasswordAction(formData: FormData) {
  const session = await getSession();
  const username = getText(formData, "username");
  const newPassword = getText(formData, "newPassword");
  const confirmPassword = getText(formData, "confirmPassword");
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

  if (!newPassword) {
    redirect(resultPath(alphabet, username, "missing-password"));
  }

  if (newPassword !== confirmPassword) {
    redirect(resultPath(alphabet, username, "password-mismatch"));
  }

  const result = await forceUserPassword(username, newPassword);
  redirect(resultPath(alphabet, username, result.ok ? "success" : result.reason));
}
