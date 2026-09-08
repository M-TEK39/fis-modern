import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import DeactivateUserForm from "@/app/users/deactivate/deactivate-user-form";
import { updateUserStatusAction } from "@/app/users/deactivate/actions";
import { getSession } from "@/lib/session";

const USER_ADMIN_ROLE = "User Administration";
const ALPHABET = /^[A-Z]$/;
type UserStatusOperation = "deactivate" | "deactivate-expired" | "activate";

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

function normalizeOperation(value: string | undefined): UserStatusOperation {
  return value === "activate" || value === "deactivate-expired" ? value : "deactivate";
}

function getMessage(result: string | undefined, operation: UserStatusOperation) {
  switch (result) {
    case "success":
      return operation === "activate"
        ? ({ tone: "success", text: "User activated." } as const)
        : ({ tone: "success", text: "User deactivated." } as const);
    case "forbidden":
      return {
        tone: "error",
        text: "You do not have permission to manage user activation.",
      } as const;
    case "missing-username":
      return { tone: "error", text: "Username is required." } as const;
    case "not-found":
      return { tone: "error", text: "The selected user could not be found." } as const;
    case "rejected":
      return operation === "deactivate-expired"
        ? ({
            tone: "error",
            text: "The user cannot be deactivated because the password is still valid.",
          } as const)
        : ({ tone: "error", text: "The user status change was rejected." } as const);
    case "unavailable":
      return {
        tone: "error",
        text: "The user status service is unavailable. Retry when the FIS API is available.",
      } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    case "invalid-operation":
      return { tone: "error", text: "Choose a valid user status action." } as const;
    default:
      return result
        ? ({ tone: "error", text: "The user status change could not be completed." } as const)
        : null;
  }
}

export default async function DeactivateUserPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/users/deactivate" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Activate or De-Activate User could not be opened.</h2>
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
          <h2>You do not have permission to manage user activation.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const username = getQueryValue(query.username) ?? getQueryValue(query.Username) ?? "";
  const alphabet = normalizeAlphabet(
    getQueryValue(query.alphabet) ?? getQueryValue(query.Alphabet),
  );
  const operation = normalizeOperation(getQueryValue(query.operation));
  const message = getMessage(getQueryValue(query.result), operation);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="deactivate-user-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">User Administration</p>
            <h1 id="deactivate-user-title">Activate / De-Activate User</h1>
            <p>Manage the active state of an existing user account.</p>
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

        <section className="vehicle-overview" aria-labelledby="deactivate-user-form-title">
          <p className="eyebrow">Account status</p>
          <h2 id="deactivate-user-form-title">
            Search for the user you want to de-activate or activate
          </h2>
          <DeactivateUserForm
            action={updateUserStatusAction}
            username={username}
            alphabet={alphabet}
          />
        </section>
      </section>
    </main>
  );
}
