"use server";

import { redirect } from "next/navigation";

import {
  changePasswordAgainstApi,
  confirmForgotPassword,
  loginAgainstApi,
  logoutAgainstApi,
  resolveAuthenticatedUsername,
  setAuthCookies,
  startForgotPassword,
} from "@/lib/auth/api-auth";

export type LoginActionState = {
  status: "idle" | "error";
  message?: string;
};

export async function loginAction(
  _previousState: LoginActionState,
  formData: FormData,
): Promise<LoginActionState> {
  const firstNameValue = formData.get("firstName");
  const passwordValue = formData.get("password");
  const rememberMeValue = formData.get("rememberMe");
  const firstName = typeof firstNameValue === "string" ? firstNameValue.trim() : "";
  const password = typeof passwordValue === "string" ? passwordValue : "";
  const rememberMe = rememberMeValue === "on";

  if (!firstName || !password) {
    return {
      status: "error",
      message: "Enter your first name and password to continue.",
    };
  }

  const result = await loginAgainstApi(firstName, password, rememberMe);
  if (!result.ok) {
    const message =
      result.reason === "invalid-credentials"
        ? "The first name or password is incorrect."
        : result.reason === "invalid-response"
          ? "The sign-in service returned an unexpected response. Please try again."
          : "The sign-in service is temporarily unavailable. Check the API and try again.";

    return { status: "error", message };
  }

  await setAuthCookies(result.cookies);

  if (result.passwordExpired) {
    redirect("/change-password");
  }

  redirect("/home");
}

export async function logoutAction() {
  await logoutAgainstApi();
  redirect("/login");
}

export type ForgotPasswordActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

export async function forgotPasswordAction(
  _previousState: ForgotPasswordActionState,
  formData: FormData,
): Promise<ForgotPasswordActionState> {
  const identifierValue = formData.get("identifier");
  const identifier = typeof identifierValue === "string" ? identifierValue.trim() : "";

  if (!identifier) {
    return { status: "error", message: "Enter your username, email address, or user code." };
  }

  const result = await startForgotPassword(identifier);
  if (!result.ok) {
    return {
      status: "error",
      message:
        result.reason === "unavailable"
          ? "Password reset is temporarily unavailable. Please try again later or contact support."
          : "Password reset returned an unexpected response. Please try again.",
    };
  }

  return {
    status: "success",
    message:
      "If an account with a registered email address exists, a password reset link has been sent.",
  };
}

export async function resetPasswordAction(
  _previousState: ForgotPasswordActionState,
  formData: FormData,
): Promise<ForgotPasswordActionState> {
  const tokenValue = formData.get("token");
  const newPasswordValue = formData.get("newPassword");
  const confirmNewPasswordValue = formData.get("confirmNewPassword");
  const token = typeof tokenValue === "string" ? tokenValue.trim() : "";
  const newPassword = typeof newPasswordValue === "string" ? newPasswordValue : "";
  const confirmNewPassword =
    typeof confirmNewPasswordValue === "string" ? confirmNewPasswordValue : "";

  if (!token) {
    return { status: "error", message: "This password reset link is invalid or has expired." };
  }

  if (!newPassword || !confirmNewPassword) {
    return { status: "error", message: "Enter and confirm your new password." };
  }

  if (newPassword.length < 8) {
    return { status: "error", message: "Password must be at least 8 characters long." };
  }

  if (newPassword !== confirmNewPassword) {
    return { status: "error", message: "The new passwords do not match." };
  }

  const result = await confirmForgotPassword(token, newPassword, confirmNewPassword);
  if (!result.ok) {
    return {
      status: "error",
      message:
        result.reason === "unavailable"
          ? "Password reset is temporarily unavailable. Please try again later."
          : result.message || "This password reset link is invalid or has expired.",
    };
  }

  return {
    status: "success",
    message: "Password reset successfully. You can now sign in with your new password.",
  };
}

export type ChangePasswordActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

export async function changePasswordAction(
  _previousState: ChangePasswordActionState,
  formData: FormData,
): Promise<ChangePasswordActionState> {
  const currentPasswordValue = formData.get("currentPassword");
  const newPasswordValue = formData.get("newPassword");
  const confirmNewPasswordValue = formData.get("confirmNewPassword");
  const currentPassword = typeof currentPasswordValue === "string" ? currentPasswordValue : "";
  const newPassword = typeof newPasswordValue === "string" ? newPasswordValue : "";
  const confirmNewPassword =
    typeof confirmNewPasswordValue === "string" ? confirmNewPasswordValue : "";

  if (!currentPassword || !newPassword || !confirmNewPassword) {
    return { status: "error", message: "Complete all password fields to continue." };
  }

  if (newPassword.length < 8) {
    return { status: "error", message: "Password must be at least 8 characters long." };
  }

  if (newPassword !== confirmNewPassword) {
    return { status: "error", message: "The new passwords do not match." };
  }

  const identity = await resolveAuthenticatedUsername();
  if (!identity.ok) {
    return {
      status: "error",
      message:
        identity.reason === "unavailable"
          ? "The sign-in service is temporarily unavailable. Please try again."
          : "Your session has expired. Sign in again to change your password.",
    };
  }

  const result = await changePasswordAgainstApi(
    identity.username,
    currentPassword,
    newPassword,
    confirmNewPassword,
  );
  if (!result.ok) {
    return {
      status: "error",
      message:
        result.reason === "unauthorized"
          ? "Your session has expired. Sign in again to change your password."
          : result.reason === "unavailable"
            ? "The sign-in service is temporarily unavailable. Please try again."
            : result.message ||
              "Password change failed. Check your current password and try again.",
    };
  }

  await logoutAgainstApi();

  return {
    status: "success",
    message: "Password changed successfully. Sign in again with your new password.",
  };
}
