"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

export default function SessionRecovery({ returnPath = "/home" }: { returnPath?: string }) {
  const router = useRouter();
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function refresh() {
      try {
        const response = await fetch("/api/auth/refresh", {
          method: "POST",
          credentials: "same-origin",
          headers: { accept: "application/json" },
        });

        if (!response.ok) {
          throw new Error("Session refresh failed");
        }

        if (!cancelled) {
          router.replace(returnPath);
          router.refresh();
        }
      } catch {
        if (!cancelled) {
          setFailed(true);
        }
      }
    }

    void refresh();
    return () => {
      cancelled = true;
    };
  }, [returnPath, router]);

  if (failed) {
    return (
      <>
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>Your session could not be refreshed. Please sign in again.</span>
        </div>
        <a className="button button-primary" href="/login">
          Return to sign in
        </a>
      </>
    );
  }

  return (
    <div className="loading-card" aria-live="polite" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Refreshing your session...</p>
    </div>
  );
}
