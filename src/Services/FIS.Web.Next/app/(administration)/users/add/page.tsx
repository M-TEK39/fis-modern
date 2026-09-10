import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/(auth)/actions/auth";
import UserAddForm from "@/app/(administration)/users/add/user-add-form";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  UserAdminApiError,
  getUserAdminPositions,
  getUserAdminSites,
  getUserAdminUserChoices,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";
import { createUserAdminAction } from "@/app/(administration)/users/add/actions";

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

function resultMessage(result: string | undefined) {
  switch (result) {
    case "success":
      return { tone: "success", text: "User created successfully." } as const;
    case "forbidden":
      return { tone: "error", text: "You do not have permission to create users." } as const;
    case "invalid":
      return {
        tone: "error",
        text: "Check the required fields and submit the complete legacy profile.",
      } as const;
    case "rejected":
      return {
        tone: "error",
        text: "The user profile was rejected. Check for an existing username, e-mail, or ID.",
      } as const;
    case "not-found":
      return { tone: "error", text: "The user profile service could not be found." } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    case "unavailable":
      return {
        tone: "error",
        text: "The user profile service is unavailable. Retry when the FIS API is available.",
      } as const;
    default:
      return result
        ? ({ tone: "error", text: "User creation could not be completed." } as const)
        : null;
  }
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to create users.</h2>
      <p className="muted-copy">This workflow requires the User Administration role.</p>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>User creation reference data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/users/add">
          Try again
        </Link>
        <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
          Menu
        </Link>
      </div>
    </section>
  );
}

export async function UserAdminAddPage({
  searchParams,
}: Readonly<{ searchParams?: SearchParams }> = {}) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/users/add" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasUserAdministrationRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const query = searchParams ? await searchParams : {};
  const message = resultMessage(getQueryValue(query.result));

  try {
    const [sites, positions, approvers] = await Promise.all([
      getUserAdminSites(),
      getUserAdminPositions(),
      getUserAdminUserChoices(),
    ]);
    const displayName =
      session.email ??
      (session.userAccessCode ? `User ${session.userAccessCode}` : "Authenticated User");

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="user-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">User Administration</p>
              <h1 id="user-add-title">Add New User</h1>
              <p>Capture a complete user profile in the established legacy sequence.</p>
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
          <UserAddForm
            action={createUserAdminAction}
            displayName={displayName}
            positions={positions}
            sites={sites}
            approvers={approvers}
          />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
              Back to Menu
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof UserAdminApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/users/add" />
        </main>
      );
    }

    console.error(
      "FIS user administration create reference data failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default UserAdminAddPage;
