import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import AdminForgotPasswordForm from "@/app/(administration)/users/admin/forgot-password/admin-forgot-password-form";
import { sendAdminPasswordReset } from "@/app/(administration)/users/admin/forgot-password/actions";
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

function getMessage(error: string | undefined) {
  switch (error) {
    case "forbidden":
      return "You do not have permission to reset another user's password.";
    case "missing-username":
      return "Username or user code is required.";
    case "unavailable":
      return "The password reset service is unavailable. Retry when the FIS API is available.";
    default:
      return error ? "The password reset request could not be completed." : null;
  }
}

async function AdminForgotPasswordPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/users/admin/forgot-password" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>User password reset could not be opened.</h2>
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
  const alphabet = getQueryValue(query.alphabet) ?? getQueryValue(query.Alphabet) ?? "A";
  const error = getMessage(getQueryValue(query.error));
  const sent = getQueryValue(query.sent) === "1";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="admin-reset-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">User Administration</p>
            <h1 id="admin-reset-title">Reset User Password</h1>
            <p>Send a secure, one-time reset link to the selected user.</p>
          </div>
          <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
            Menu
          </Link>
        </header>

        {sent ? (
          <div className="notice notice-success" role="status">
            If the account has a registered email address, a password reset link has been sent.
          </div>
        ) : null}
        {error ? (
          <div className="notice notice-error" role="alert">
            {error}
          </div>
        ) : null}

        <section className="vehicle-overview" aria-labelledby="admin-reset-form-title">
          <p className="eyebrow">Account recovery</p>
          <h2 id="admin-reset-form-title">Choose a user</h2>
          <AdminForgotPasswordForm
            action={sendAdminPasswordReset}
            username={username}
            alphabet={alphabet}
          />
        </section>
      </section>
    </main>
  );
}

export default function AdminForgotPasswordPage(
  props: NonNullable<Parameters<typeof AdminForgotPasswordPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <AdminForgotPasswordPageContent {...props} />
    </Suspense>
  );
}
