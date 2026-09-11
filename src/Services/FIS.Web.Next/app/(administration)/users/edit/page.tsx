import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import UserEditForm from "@/app/(administration)/users/edit/user-edit-form";
import UserEditSearchForm from "@/app/(administration)/users/edit/user-edit-search-form";
import { updateUserAdminAction } from "@/app/(administration)/users/edit/actions";
import {
  UserAdminApiError,
  getUserAdminPositions,
  getUserAdminSites,
  getUserAdminUserChoices,
  type UserAdminProfile,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

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

function normalizeAlphabet(value: string | undefined) {
  const candidate = value?.trim().toUpperCase() ?? "";
  return /^[A-Z]$/.test(candidate) ? candidate : "A";
}

function resultMessage(result: string | undefined) {
  switch (result) {
    case "success":
      return { tone: "success", text: "User profile updated successfully." } as const;
    case "forbidden":
      return { tone: "error", text: "You do not have permission to update users." } as const;
    case "invalid":
      return {
        tone: "error",
        text: "Check the required fields and submit the complete legacy profile.",
      } as const;
    case "rejected":
      return {
        tone: "error",
        text: "The user profile update was rejected. Check the submitted values.",
      } as const;
    case "not-found":
      return { tone: "error", text: "The selected user profile could not be found." } as const;
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
        ? ({ tone: "error", text: "The user profile update could not be completed." } as const)
        : null;
  }
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to update users.</h2>
      <p className="muted-copy">This workflow requires the User Administration role.</p>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>User profiles could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/users/edit">
          Try again
        </Link>
        <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
          Menu
        </Link>
      </div>
    </section>
  );
}

function findSelectedProfile(profiles: readonly UserAdminProfile[], username: string) {
  return (
    profiles.find(
      (profile) =>
        profile.userName?.localeCompare(username, undefined, { sensitivity: "accent" }) === 0,
    ) ?? null
  );
}

function UserAdminEditView({
  message,
  username,
  alphabet,
  userChoices,
  selectedProfile,
  displayName,
  sites,
  positions,
  profiles,
}: Readonly<{
  message: ReturnType<typeof resultMessage>;
  username: string;
  alphabet: string;
  userChoices: readonly UserAdminProfile[];
  selectedProfile: UserAdminProfile | null;
  displayName: string;
  sites: Awaited<ReturnType<typeof getUserAdminSites>>;
  positions: Awaited<ReturnType<typeof getUserAdminPositions>>;
  profiles: Awaited<ReturnType<typeof getUserAdminUserChoices>>;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="user-edit-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">User Administration</p>
            <h1 id="user-edit-title">Update or Modify User Details</h1>
            <p>Search for a username, then update its complete legacy profile.</p>
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
        <section className="vehicle-form-section" aria-labelledby="user-edit-search-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Find profile</p>
              <h2 id="user-edit-search-title">Select a username</h2>
            </div>
          </div>
          <UserEditSearchForm alphabet={alphabet} users={userChoices} username={username} />
        </section>
        {username && !selectedProfile ? (
          <div className="notice notice-error" role="alert">
            No profile could be found for user {username}.
          </div>
        ) : null}
        {selectedProfile ? (
          <UserEditForm
            action={updateUserAdminAction}
            alphabet={alphabet}
            approvers={profiles}
            displayName={displayName}
            positions={positions}
            profile={selectedProfile}
            sites={sites}
          />
        ) : null}
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
}

async function UserAdminEditPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/users/edit" />
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

  const query = await searchParams;
  const username = (getQueryValue(query.Username) ?? getQueryValue(query.username) ?? "").trim();
  const alphabet = normalizeAlphabet(
    getQueryValue(query.Alphabet) ?? getQueryValue(query.alphabet),
  );
  const message = resultMessage(getQueryValue(query.result));

  try {
    const [sites, positions, profiles] = await Promise.all([
      getUserAdminSites(),
      getUserAdminPositions(),
      getUserAdminUserChoices(),
    ]);
    const selectedProfile = username ? findSelectedProfile(profiles, username) : null;
    const userChoices = profiles.filter((profile) => profile.userName);
    const displayName =
      session.email ??
      (session.userAccessCode ? `User ${session.userAccessCode}` : "Authenticated User");

    return (
      <UserAdminEditView
        message={message}
        username={username}
        alphabet={alphabet}
        userChoices={userChoices}
        selectedProfile={selectedProfile}
        displayName={displayName}
        sites={sites}
        positions={positions}
        profiles={profiles}
      />
    );
  } catch (error) {
    if (error instanceof UserAdminApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/users/edit" />
        </main>
      );
    }

    console.error(
      "FIS user administration edit reference data failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function UserAdminEditPage(
  props: NonNullable<Parameters<typeof UserAdminEditPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <UserAdminEditPageContent {...props} />
    </Suspense>
  );
}
