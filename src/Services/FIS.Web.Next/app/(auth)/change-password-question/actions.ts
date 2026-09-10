"use server";

import { redirect } from "next/navigation";

import { changePasswordQuestionAgainstApi } from "@/lib/auth/api-auth";
import { getSession } from "@/lib/auth/session";

const USER_ADMIN_ROLE = "User Administration";
const RETURN_PATH = "/change-password-question";

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function resultPath(username: string, result: string) {
  const params = new URLSearchParams({ username, result });
  return `${RETURN_PATH}?${params.toString()}`;
}

function getAuthenticatedIdentifier(userAccessCode: string | undefined, email: string | undefined) {
  return userAccessCode?.trim() || email?.trim() || "";
}

export async function changePasswordQuestionAction(formData: FormData) {
  const session = await getSession();
  const requestedUsername = getText(formData, "username");
  const oldPassword = getText(formData, "oldPassword");
  const newPassword = getText(formData, "newPassword");
  const confirmNewPassword = getText(formData, "confirmNewPassword");
  const securityQuestion = getText(formData, "securityQuestion");
  const securityAnswer = getText(formData, "securityAnswer");
  const email = getText(formData, "email");
  const isAdministrator =
    session.status === "authenticated" && hasUserAdministrationRole(session.roles);
  const username = isAdministrator
    ? requestedUsername
    : session.status === "authenticated"
      ? getAuthenticatedIdentifier(session.userAccessCode, session.email)
      : requestedUsername;

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status !== "authenticated") {
    redirect(resultPath(username, "unavailable"));
  }

  if (!username) {
    redirect(resultPath(username, "missing-username"));
  }

  if (!oldPassword || !newPassword || !confirmNewPassword) {
    redirect(resultPath(username, "missing-password"));
  }

  if (newPassword !== confirmNewPassword) {
    redirect(resultPath(username, "password-mismatch"));
  }

  if (!securityQuestion || securityQuestion === "Select Question...") {
    redirect(resultPath(username, "missing-question"));
  }

  if (!securityAnswer) {
    redirect(resultPath(username, "missing-answer"));
  }

  if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    redirect(resultPath(username, "invalid-email"));
  }

  const result = await changePasswordQuestionAgainstApi(
    username,
    oldPassword,
    newPassword,
    confirmNewPassword,
    securityQuestion,
    securityAnswer,
    email,
  );

  if (result.ok) {
    redirect(resultPath(username, "success"));
  }

  redirect(resultPath(username, result.reason));
}
