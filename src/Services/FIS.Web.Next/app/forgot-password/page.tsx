import Link from "next/link";

import ForgotPasswordForm from "@/app/forgot-password/forgot-password-form";

function Brand() {
  return (
    <div className="brand">
      <div className="brand-mark" aria-hidden="true">
        FIS
      </div>
      <div>
        <p className="brand-name">Fleet Information System</p>
        <p className="brand-caption">Gauteng Provincial Government</p>
      </div>
    </div>
  );
}

export default function ForgotPasswordPage() {
  return (
    <main className="page-shell">
      <section className="auth-card" aria-labelledby="forgot-password-title">
        <Brand />
        <div className="auth-header">
          <p className="eyebrow">Account recovery</p>
          <h1 id="forgot-password-title">Forgot your password?</h1>
          <p>We will email a secure, one-time password reset link if the account has a registered email address.</p>
        </div>
        <ForgotPasswordForm />
        <div className="auth-footer">
          <Link className="text-link" href="/login">
            Back to sign in
          </Link>
          <span className="auth-footnote">For security, the same response is shown whether or not an account exists.</span>
        </div>
      </section>
    </main>
  );
}
