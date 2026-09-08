import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import ChangePasswordQuestionForm from "@/app/change-password-question/change-password-question-form";
import { changePasswordQuestionAction } from "@/app/change-password-question/actions";
import { getUserAdminUserChoices, UserAdminApiError } from "@/lib/api-user-admin";
import { getSession } from "@/lib/session";

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

export default async function ChangePasswordQuestionPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/change-password-question" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Change Password and Question could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
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
          <main className="page-shell vehicle-page-shell">
            <section className="vehicle-status-card" role="alert">
              <p className="eyebrow">Access restricted</p>
              <h2>You do not have permission to load user accounts.</h2>
            </section>
          </main>
        );
      }

      console.error(
        "FIS password/question user lookup failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">API unavailable</p>
            <h2>Users could not be loaded.</h2>
            <p className="muted-copy">Retry when the FIS API is available.</p>
          </section>
        </main>
      );
    }
  }

  const selectedUser = users.find(
    (user) => user.userName?.toLowerCase() === username.toLowerCase(),
  );
  const email = selectedUser?.email ?? session.email ?? "";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="change-password-question-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Password security</p>
            <h1 id="change-password-question-title">Change Password and Question</h1>
            <p>Update the password and recovery question used by the FIS account.</p>
          </div>
          <Link
            className="button button-secondary"
            href={canManageOthers ? "/UserAdmin/UserAdminMenu.aspx" : "/home"}
          >
            {canManageOthers ? "Menu" : "Home"}
          </Link>
        </header>

        <p className="muted-copy">
          The security answer is case sensitive. Keep it safe so the password recovery process
          remains available.
        </p>
        {message ? (
          <div
            className={`notice notice-${message.tone}`}
            role={message.tone === "error" ? "alert" : "status"}
          >
            {message.text}
          </div>
        ) : null}

        <section className="vehicle-overview" aria-labelledby="change-password-question-form-title">
          <p className="eyebrow">Account credentials</p>
          <h2 id="change-password-question-form-title">Enter the required account details</h2>
          <ChangePasswordQuestionForm
            action={changePasswordQuestionAction}
            canManageOthers={canManageOthers}
            users={users}
            username={username}
            email={email}
          />
        </section>
      </section>
    </main>
  );
}
