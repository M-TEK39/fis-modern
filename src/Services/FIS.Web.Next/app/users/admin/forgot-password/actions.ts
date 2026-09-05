"use server";

import { redirect } from "next/navigation";

import { startForgotPassword } from "@/lib/api-auth";
import { getSession } from "@/lib/session";

const USER_ADMIN_ROLE = "User Administration";

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

export async function sendAdminPasswordReset(formData: FormData) {
  const session = await getSession();
  const username = getText(formData, "username");
  const alphabet = getText(formData, "alphabet") || "A";
  const returnPath = "/users/admin/forgot-password";

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status !== "authenticated") {
    redirect(`${returnPath}?error=unavailable`);
  }

  if (!hasUserAdministrationRole(session.roles)) {
    redirect(`${returnPath}?error=forbidden`);
  }

  if (!username) {
    redirect(`${returnPath}?alphabet=${encodeURIComponent(alphabet)}&error=missing-username`);
  }

  const result = await startForgotPassword(username);
  if (result.ok) {
    redirect(`${returnPath}?alphabet=${encodeURIComponent(alphabet)}&sent=1`);
  }

  redirect(`${returnPath}?alphabet=${encodeURIComponent(alphabet)}&error=${result.reason}`);
}
