import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ResetLoginForm from "@/app/(administration)/users/reset-login/reset-login-form";
import { resetUserLoginAction } from "@/app/(administration)/users/reset-login/actions";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

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
      return { tone: "success", text: "Login reset successfully." } as const;
    case "forbidden":
      return {
        tone: "error",
        text: "You do not have permission to reset another user's login.",
      } as const;
    case "missing-username":
      return { tone: "error", text: "Username is required." } as const;
    case "unavailable":
      return {
        tone: "error",
        text: "The login reset service is unavailable. Retry when the FIS API is available.",
      } as const;
    case "unauthorized":
      return {
        tone: "error",
        text: "Your session is no longer authorized. Sign in again.",
      } as const;
    default:
      return result
        ? ({ tone: "error", text: "The login reset request could not be completed." } as const)
        : null;
  }
}

async function ResetLoginPageContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/users/reset-login" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Reset Login could not be opened.</h2>
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
          <h2>You do not have permission to reset user logins.</h2>
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

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="reset-login-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">User Administration</p>
            <h1 id="reset-login-title">Reset Login</h1>
            <p>Reset the password counter and unlock an existing user account.</p>
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

        <section className="vehicle-overview" aria-labelledby="reset-login-form-title">
          <p className="eyebrow">Account access</p>
          <h2 id="reset-login-form-title">Select the user you want to reset</h2>
          <ResetLoginForm action={resetUserLoginAction} username={username} alphabet={alphabet} />
        </section>
      </section>
    </main>
  );
}

export default function ResetLoginPage(
  props: NonNullable<Parameters<typeof ResetLoginPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ResetLoginPageContent {...props} />
    </Suspense>
  );
}
