import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ForcePasswordForm from "@/app/(administration)/users/force-password-change/force-password-form";
import { forceUserPasswordAction } from "@/app/(administration)/users/force-password-change/actions";
import {
  getUserAdminUserChoices,
  UserAdminApiError,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";

const USER_ADMIN_ROLE = "User Administration";
const ALPHABET = /^[A-Z]$/;

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function normalizeAlphabet(value: string | undefined) {
  const candidate = value?.trim().toUpperCase() ?? "";
  return ALPHABET.test(candidate) ? candidate : "A";
}

function getMessage(result: string | undefined) {
  switch (result) {
    case "success":
      return { tone: "success", text: "Password reset successfully." } as const;
    case "forbidden":
      return {
        tone: "error",
        text: "You do not have permission to reset user passwords.",
      } as const;
    case "missing-username":
      return { tone: "error", text: "Please select a username." } as const;
    case "missing-password":
      return { tone: "error", text: "New password is required." } as const;
    case "password-mismatch":
      return {
        tone: "error",
        text: "The Confirm Password must match the New Password entry.",
      } as const;
    case "not-found":
      return { tone: "error", text: "The selected user could not be found." } as const;
    case "rejected":
      return {
        tone: "error",
        text: "The new password does not meet the FIS password policy.",
      } as const;
    case "unavailable":
      return {
        tone: "error",
        text: "The password reset service is unavailable. Retry when the FIS API is available.",
      } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    default:
      return result
        ? ({ tone: "error", text: "The password reset could not be completed." } as const)
        : null;
  }
}

export default async function ForcePasswordChangePage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/users/force-password-change" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Reset User Password could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }
  if (!hasUserAdministrationRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to reset user passwords.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const username = getQueryValue(query.username) ?? getQueryValue(query.Username) ?? "";
  const alphabet = normalizeAlphabet(
    getQueryValue(query.alphabet) ?? getQueryValue(query.Alphabet),
  );
  const message = getMessage(getQueryValue(query.result));

  try {
    const users = await getUserAdminUserChoices();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="force-password-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">User Administration</p>
              <h1 id="force-password-title">Reset User Password</h1>
              <p>Set a new password for an existing user account.</p>
            </div>
            <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
              Menu
            </Link>
          </header>

          {message ? (
            <div
              className={`notice notice-${message.tone}`}
              role={message.tone === "error" ? "alert" : "status"}
            >
              {message.text}
            </div>
          ) : null}

          <section className="vehicle-overview" aria-labelledby="force-password-form-title">
            <p className="eyebrow">Password administration</p>
            <h2 id="force-password-form-title">Choose the user and enter a new password</h2>
            <ForcePasswordForm
              action={forceUserPasswordAction}
              users={users}
              username={username}
              alphabet={alphabet}
            />
          </section>
        </section>
      </main>
    );
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
      "FIS force-password user lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Reset User Password could not be loaded.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }
}
