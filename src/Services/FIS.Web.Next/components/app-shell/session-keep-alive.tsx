"use client";

import { useEffect, useRef } from "react";

const ACCESS_TOKEN_LIFETIME_MS = 15 * 60 * 1000;
const REFRESH_LEAD_TIME_MS = 3 * 60 * 1000;
const REFRESH_AFTER_MS = ACCESS_TOKEN_LIFETIME_MS - REFRESH_LEAD_TIME_MS;
const ACTIVITY_REFRESH_THROTTLE_MS = 5 * 60 * 1000;
const REFRESH_REQUEST_TIMEOUT_MS = 20 * 1000;

let inFlightRefresh: Promise<boolean> | null = null;
let lastRefreshAttemptAt = 0;

/**
 * Keep refresh requests shared across the recovery screen and the app shell.
 * The refresh cookie remains HttpOnly and is sent only by the same-origin
 * browser request; no token or cookie value is read in this module.
 */
export function requestSessionRefresh(): Promise<boolean> {
  if (inFlightRefresh) {
    return inFlightRefresh;
  }

  lastRefreshAttemptAt = Date.now();
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), REFRESH_REQUEST_TIMEOUT_MS);

  inFlightRefresh = fetch("/api/auth/refresh", {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: { accept: "application/json" },
    signal: controller.signal,
  })
    .then((response) => response.ok)
    .catch(() => false)
    .finally(() => {
      window.clearTimeout(timeout);
      inFlightRefresh = null;
    });

  return inFlightRefresh;
}

/**
 * Mount once inside the authenticated server-rendered shell. It deliberately
 * renders no UI and only keeps an existing session warm while the page is in
 * use.
 */
export default function SessionKeepAlive() {
  const timerRef = useRef<number | null>(null);
  const inFlightRef = useRef<Promise<boolean> | null>(null);
  const mountedAtRef = useRef(0);

  useEffect(() => {
    let mounted = true;

    const clearScheduledRefresh = () => {
      if (timerRef.current === null) {
        return;
      }

      window.clearTimeout(timerRef.current);
      timerRef.current = null;
    };

    const scheduleRefresh = () => {
      if (!mounted || document.visibilityState === "hidden") {
        return;
      }

      clearScheduledRefresh();
      const lastActivityAt = Math.max(mountedAtRef.current, lastRefreshAttemptAt);
      const elapsed = Date.now() - lastActivityAt;
      const delay = Math.max(0, REFRESH_AFTER_MS - elapsed);
      timerRef.current = window.setTimeout(() => {
        timerRef.current = null;
        startRefresh();
      }, delay);
    };

    const startRefresh = () => {
      if (!mounted || document.visibilityState === "hidden" || inFlightRef.current !== null) {
        return;
      }

      const elapsed = Date.now() - Math.max(mountedAtRef.current, lastRefreshAttemptAt);
      if (elapsed < ACTIVITY_REFRESH_THROTTLE_MS) {
        scheduleRefresh();
        return;
      }

      clearScheduledRefresh();

      const request = requestSessionRefresh();
      inFlightRef.current = request;
      void request.finally(() => {
        if (inFlightRef.current === request) {
          inFlightRef.current = null;
        }

        if (mounted) {
          scheduleRefresh();
        }
      });
    };

    const refreshOnActivity = () => {
      if (!mounted || document.visibilityState === "hidden") {
        return;
      }

      const elapsed = Date.now() - Math.max(mountedAtRef.current, lastRefreshAttemptAt);
      if (elapsed < ACTIVITY_REFRESH_THROTTLE_MS) {
        scheduleRefresh();
        return;
      }

      startRefresh();
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === "hidden") {
        clearScheduledRefresh();
        return;
      }

      refreshOnActivity();
    };

    mountedAtRef.current = Date.now();
    document.addEventListener("visibilitychange", handleVisibilityChange);
    window.addEventListener("focus", refreshOnActivity);
    scheduleRefresh();

    return () => {
      mounted = false;
      clearScheduledRefresh();
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      window.removeEventListener("focus", refreshOnActivity);
    };
  }, []);

  return null;
}
