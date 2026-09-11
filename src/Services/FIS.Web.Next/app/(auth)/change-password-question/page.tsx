import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import {
  AuthBrand,
  AuthFooter,
  AuthHeader,
  AuthNotice,
  AuthPage,
} from "@/components/app-shell/auth-ui";
import ChangePasswordQuestionForm from "@/app/(auth)/change-password-question/change-password-question-form";
import { changePasswordQuestionAction } from "@/app/(auth)/change-password-question/actions";
import ChangePasswordQuestionSessionRecovery from "@/app/(auth)/change-password-question/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import {
  getUserAdminUserChoices,
  UserAdminApiError,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";

const USER_ADMIN_ROLE = "User Administration";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function getMessage(result: string | undefined) {
  switch (result) {
    case "success":
      return {
        tone: "success",
        text: "Password and security question changed successfully.",
      } as const;
    case "missing-username":
      return { tone: "error", text: "Please select a username." } as const;
    case "missing-password":
      return {
        tone: "error",
        text: "Complete the old, new, and confirmation password fields.",
      } as const;
    case "password-mismatch":
      return {
        tone: "error",
        text: "The Confirm New Password must match the New Password entry.",
      } as const;
    case "missing-question":
      return { tone: "error", text: "Security question is required." } as const;
    case "missing-answer":
      return { tone: "error", text: "Security answer is required." } as const;
    case "invalid-email":
      return { tone: "error", text: "Please enter a valid email address." } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    case "unavailable":
      return {
        tone: "error",
        text: "The password/question service is unavailable. Retry when the FIS API is available.",
      } as const;
    case "invalid-response":
      return {
        tone: "error",
        text: "The password/question update failed. Check the old password and try again.",
      } as const;
    default:
      return result
        ? ({ tone: "error", text: "The password/question update could not be completed." } as const)
        : null;
  }
}

function StatusState({
  id,
  title,
  description,
  message,
}: Readonly<{
  id: string;
  title: string;
  description: string;
  message: string;
}>) {
  return (
    <>
      <AuthBrand caption="Secure account settings" />
      <AuthHeader id={id} title={title} description={description} />
      <AuthNotice tone="error">{message}</AuthNotice>
    </>
  );
}

async function ChangePasswordQuestionContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <AuthPage>
        <AuthBrand caption="Secure account settings" />
        <AuthHeader
          id="change-password-question-expired-title"
          title="Continue to change your password and question"
          description="Your session has expired. Refresh it or sign in again to continue."
        />
        <ChangePasswordQuestionSessionRecovery returnPath="/change-password-question" />
      </AuthPage>
    );
  }
  if (session.status === "unavailable") {
    return (
      <AuthPage>
        <StatusState
          id="change-password-question-unavailable-title"
          title="Change Password and Question could not be opened."
          description="The password and recovery question can be updated when the FIS API is available."
          message="Retry when the FIS API is available."
        />
      </AuthPage>
    );
  }

  const query = await searchParams;
  const canManageOthers = hasUserAdministrationRole(session.roles);
  const requestedUsername = getQueryValue(query.username) ?? getQueryValue(query.Username) ?? "";
  const username = canManageOthers
    ? requestedUsername
    : session.userAccessCode?.trim() || session.email?.trim() || "";
  const message = getMessage(getQueryValue(query.result));
  let users: Awaited<ReturnType<typeof getUserAdminUserChoices>> = [];

  if (canManageOthers) {
    try {
      users = await getUserAdminUserChoices();
    } catch (error) {
      if (error instanceof UserAdminApiError && error.reason === "unauthorized") {
        return (
          <AuthPage>
            <StatusState
              id="change-password-question-access-title"
              title="Change Password and Question"
              description="Update the password and recovery question used by the FIS account."
              message="You do not have permission to load user accounts."
            />
          </AuthPage>
        );
      }

      console.error(
        "FIS password/question user lookup failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return (
        <AuthPage>
          <StatusState
            id="change-password-question-users-unavailable-title"
            title="Users could not be loaded."
            description="User accounts are needed to update another user’s password and question."
            message="Retry when the FIS API is available."
          />
        </AuthPage>
      );
    }
  }

  const selectedUser = users.find(
    (user) => user.userName?.toLowerCase() === username.toLowerCase(),
  );
  const email = selectedUser?.email ?? session.email ?? "";

  return (
    <AuthPage>
      <AuthBrand caption="Secure account settings" />
      <AuthHeader
        id="change-password-question-title"
        title="Change Password and Question"
        description="Update the password and recovery question used by the FIS account."
      />

      <p className="mb-6 text-center text-xs leading-relaxed text-muted-foreground">
        The security answer is case sensitive. Keep it safe so the password recovery process remains
        available.
      </p>
      {message ? <AuthNotice tone={message.tone}>{message.text}</AuthNotice> : null}

      <section aria-labelledby="change-password-question-form-title">
        <div className="mb-6 text-center">
          <p className="text-xs font-medium uppercase tracking-[0.11em] text-muted-foreground">
            Account credentials
          </p>
          <h2 id="change-password-question-form-title" className="mt-1 text-base font-semibold">
            Enter the required account details
          </h2>
        </div>
        <ChangePasswordQuestionForm
          action={changePasswordQuestionAction}
          canManageOthers={canManageOthers}
          users={users}
          username={username}
          email={email}
        />
      </section>

      <AuthFooter>
        <Link
          className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          href={canManageOthers ? "/UserAdmin/UserAdminMenu.aspx" : "/home"}
        >
          {canManageOthers ? "Menu" : "Home"}
        </Link>
      </AuthFooter>
    </AuthPage>
  );
}

export default function ChangePasswordQuestionPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ChangePasswordQuestionContent searchParams={searchParams} />
    </Suspense>
  );
}
