"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";

import { requestSessionRefresh } from "@/components/app-shell/session-keep-alive";

export default function SessionRecovery({ returnPath = "/home" }: { returnPath?: string }) {
  const router = useRouter();
  const [pending, setPending] = useState(true);
  const [failed, setFailed] = useState(false);
  const mountedRef = useRef(true);
  const inFlightRef = useRef<Promise<boolean> | null>(null);

  const refreshSession = useCallback(async () => {
    setPending(true);
    setFailed(false);

    const request = inFlightRef.current ?? requestSessionRefresh();
    inFlightRef.current = request;

    try {
      if (!(await request)) {
        if (mountedRef.current) {
          setFailed(true);
        }
        return;
      }

      if (mountedRef.current) {
        router.replace(returnPath);
        router.refresh();
      }
    } finally {
      if (inFlightRef.current === request) {
        inFlightRef.current = null;
      }

      if (mountedRef.current) {
        setPending(false);
      }
    }
  }, [returnPath, router]);

  useEffect(() => {
    mountedRef.current = true;
    void refreshSession();

    return () => {
      mountedRef.current = false;
    };
  }, [refreshSession]);

  return (
    <div className="form-stack">
      {failed ? (
        <div id="session-recovery-error" className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>Your session could not be refreshed. Please try again or sign in again.</span>
        </div>
      ) : null}
      <div
        className="loading-card"
        role="status"
        aria-live="polite"
        aria-atomic="true"
        aria-busy={pending}
      >
        {pending ? <span className="spinner" aria-hidden="true" /> : null}
        <p>
          {pending
            ? "Restoring your session..."
            : failed
              ? "Session restoration was not completed."
              : "Your session has expired."}
        </p>
      </div>
      <div className="button-row">
        <button
          className="button button-primary"
          type="button"
          onClick={() => void refreshSession()}
          disabled={pending}
          aria-describedby={failed ? "session-recovery-error" : undefined}
        >
          {pending ? "Restoring..." : "Try again"}
        </button>
        <Link className="button button-secondary" href="/login">
          Return to sign in
        </Link>
      </div>
    </div>
  );
}
