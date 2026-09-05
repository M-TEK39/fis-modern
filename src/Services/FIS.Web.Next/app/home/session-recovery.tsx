"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

export default function SessionRecovery({ returnPath = "/home" }: { returnPath?: string }) {
  const router = useRouter();
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);

  async function refreshSession() {
    setPending(true);
    setFailed(false);

    try {
      const response = await fetch("/api/auth/refresh", {
        method: "POST",
        credentials: "same-origin",
        headers: { accept: "application/json" },
      });

      if (!response.ok) {
        throw new Error("Session refresh failed");
      }

      router.replace(returnPath);
      router.refresh();
    } catch {
      setFailed(true);
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="form-stack" aria-live="polite">
      {failed ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>Your session could not be refreshed. Please try again or sign in again.</span>
        </div>
      ) : null}
      <div className="loading-card" aria-busy={pending}>
        {pending ? <span className="spinner" aria-hidden="true" /> : null}
        <p>{pending ? "Refreshing your session..." : "Your session has expired."}</p>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="button" onClick={() => void refreshSession()} disabled={pending}>
          {pending ? "Retrying..." : "Try again"}
        </button>
        <Link className="button button-secondary" href="/login">
          Return to sign in
        </Link>
      </div>
    </div>
  );
}
