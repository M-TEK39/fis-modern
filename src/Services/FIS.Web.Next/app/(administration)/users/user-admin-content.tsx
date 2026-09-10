import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import {
  UserAdminApiError,
  getUserAdminProfiles,
  type UserAdminProfile,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";

const USER_ADMIN_ROLE = "User Administration";
const ALPHABET = [..."ABCDEFGHIJKLMNOPQRSTUVWXYZ"];

export type UserAdminSearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasUserAdministrationRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(USER_ADMIN_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

const LEGACY_SETTINGS_REDIRECTS: Readonly<Record<string, string>> = {
  "users/add": "/users/add",
  "users/edit": "/users/edit",
  "users/reset-login": "/users/reset-login",
  "users/deactivate": "/users/deactivate",
  "users/view": "/users/view",
  "users/view-users": "/users/view",
  "users/change-password-question": "/change-password-question",
  "users/reset-password": "/users/force-password-change",
  "users/forgot-password": "/users/admin/forgot-password",
};

function normalizeAlphabet(value: string | undefined) {
  const candidate = value?.trim().toUpperCase();
  return candidate && candidate.length === 1 && ALPHABET.includes(candidate) ? candidate : "A";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access User Administration.</h2>
      <p className="muted-copy">This menu requires the User Administration role.</p>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>User Administration could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function MenuLink({ href, children }: Readonly<{ href: string; children: React.ReactNode }>) {
  return (
    <Link className="vehicle-menu-link" href={href}>
      {children}
    </Link>
  );
}

function getLegacySettingsRedirect(query: Record<string, string | string[] | undefined>) {
  const settings = getQueryValue(query.settings)?.trim().replace(/^\/+/, "").toLowerCase();
  if (!settings || settings === "users") return null;

  const path = LEGACY_SETTINGS_REDIRECTS[settings];
  if (!path) return null;

  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key.toLowerCase() === "settings" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) {
      params.append(key, item);
    }
  }

  const queryString = params.toString();
  return queryString ? `${path}?${queryString}` : path;
}

export async function UserAdminMenuPage({
  searchParams,
}: Readonly<{ searchParams?: UserAdminSearchParams }> = {}) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/UserAdmin/UserAdminMenu.aspx" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath="/UserAdmin/UserAdminMenu.aspx" />
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
  const legacyRedirect = getLegacySettingsRedirect(query);
  if (legacyRedirect) redirect(legacyRedirect);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="user-admin-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">User Administration</p>
            <h1 id="user-admin-title">User Admin Menu</h1>
            <p>Manage user profiles and account access using the established FIS sequence.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <MenuSection title="User Admin Menu">
            <MenuLink href="/users/add">1) Add New User</MenuLink>
            <MenuLink href="/users/edit">2) Update or Modify User Details</MenuLink>
            <MenuLink href="/users/reset-login">3) Reset User Password Counter</MenuLink>
            <MenuLink href="/users/deactivate">4) Delete (or De-Activate) User</MenuLink>
            <MenuLink href="/UserAdmin/UserAdmin.aspx?Alphabet=A">5) View User Details</MenuLink>
            <MenuLink href="/change-password-question">
              6) Change Password and Password Question
            </MenuLink>
            <MenuLink href="/users/force-password-change">7) Force Password Change</MenuLink>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}

function alphabetHref(letter: string) {
  return `/UserAdmin/UserAdmin.aspx?Alphabet=${letter}`;
}

function userActionHref(path: string, user: UserAdminProfile, alphabet: string) {
  const username = user.userName ?? String(user.userAccessCode);
  const params = new URLSearchParams({ Username: username, Alphabet: alphabet });
  return `${path}?${params.toString()}`;
}

function UserRows({ users, alphabet }: Readonly<{ users: UserAdminProfile[]; alphabet: string }>) {
  if (users.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No users found</p>
        <h2>No active users matched the selected character.</h2>
        <p className="muted-copy">Choose another letter or add a new user.</p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Active FIS users filtered by last name</caption>
        <thead>
          <tr>
            <th scope="col">Edit</th>
            <th scope="col">De-Activate</th>
            <th scope="col">Reset Login</th>
            <th scope="col">Reset Password</th>
            <th scope="col">Lastname</th>
            <th scope="col">Firstname</th>
            <th scope="col">User Name</th>
            <th scope="col">Site Name</th>
            <th scope="col">Position</th>
            <th scope="col">Telephone</th>
            <th scope="col">LastLoginDate</th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={user.userAccessCode}>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={userActionHref("/users/edit", user, alphabet)}
                >
                  Edit
                </Link>
              </td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={userActionHref("/users/deactivate", user, alphabet)}
                >
                  Open
                </Link>
              </td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={userActionHref("/users/reset-login", user, alphabet)}
                >
                  Reset
                </Link>
              </td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={userActionHref("/users/admin/forgot-password", user, alphabet)}
                >
                  Reset
                </Link>
              </td>
              <td>{valueOrDash(user.lastName)}</td>
              <td>{valueOrDash(user.firstName)}</td>
              <td>{valueOrDash(user.userName)}</td>
              <td>{valueOrDash(user.siteName ?? user.siteCode)}</td>
              <td>{valueOrDash(user.positionName ?? user.positionCode)}</td>
              <td>{valueOrDash(user.telephone)}</td>
              <td>{valueOrDash(user.lastLogOn)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export async function UserAdminListPage({
  searchParams,
  routePath = "/UserAdmin/UserAdmin.aspx",
}: Readonly<{ searchParams: UserAdminSearchParams; routePath?: string }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
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
  const alphabet = normalizeAlphabet(
    getQueryValue(query.Alphabet) ?? getQueryValue(query.alphabet),
  );

  try {
    const users = await getUserAdminProfiles(alphabet);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="user-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">User Administration</p>
              <h1 id="user-list-title">View Users&apos; Details</h1>
              <p>View active users by the first character of their last name.</p>
            </div>
            <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
              Menu
            </Link>
          </header>

          <section className="vehicle-overview" aria-labelledby="alphabet-filter-title">
            <div className="vehicle-overview-header">
              <div>
                <p className="eyebrow">Alphabetical filter</p>
                <h2 id="alphabet-filter-title">Selected character: {alphabet}</h2>
              </div>
              <Link className="button button-primary" href="/users/add">
                Add a New User
              </Link>
            </div>
            <nav className="vehicle-menu-body" aria-label="Filter users by last-name initial">
              <div className="button-row">
                {ALPHABET.map((letter) => (
                  <Link
                    className={
                      letter === alphabet
                        ? "button button-primary button-small"
                        : "button button-secondary button-small"
                    }
                    href={alphabetHref(letter)}
                    key={letter}
                    aria-current={letter === alphabet ? "page" : undefined}
                  >
                    {letter}
                  </Link>
                ))}
              </div>
            </nav>
            <p className="muted-copy">
              View users by clicking on a character. Showing {users.length} active user(s).
            </p>
          </section>

          <section className="vehicle-overview" aria-labelledby="user-list-results-title">
            <div className="vehicle-overview-header">
              <div>
                <p className="eyebrow">User directory</p>
                <h2 id="user-list-results-title">Users whose last name starts with {alphabet}</h2>
              </div>
            </div>
            <UserRows users={users} alphabet={alphabet} />
          </section>

          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
              Back to Menu
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof UserAdminApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <AccessRestricted />
        </main>
      );
    }

    console.error(
      "FIS user administration lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}
