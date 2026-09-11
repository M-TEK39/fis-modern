"use client";

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
 * Subscribe the authenticated shell to visibility/focus activity and schedule
 * shared refresh requests. The returned cleanup removes every browser
 * subscription and cancels any refresh timer created by this subscription.
 */
export function subscribeToSessionKeepAlive(): () => void {
  let mounted = true;
  let timer: number | null = null;
  let inFlight: Promise<boolean> | null = null;
  const mountedAt = Date.now();

  const clearScheduledRefresh = () => {
    if (timer === null) {
      return;
    }

    window.clearTimeout(timer);
    timer = null;
  };

  const scheduleRefresh = () => {
    if (!mounted || document.visibilityState === "hidden") {
      return;
    }

    clearScheduledRefresh();
    const lastActivityAt = Math.max(mountedAt, lastRefreshAttemptAt);
    const elapsed = Date.now() - lastActivityAt;
    const delay = Math.max(0, REFRESH_AFTER_MS - elapsed);
    timer = window.setTimeout(() => {
      timer = null;
      startRefresh();
    }, delay);
  };

  const startRefresh = () => {
    if (!mounted || document.visibilityState === "hidden" || inFlight !== null) {
      return;
    }

    const elapsed = Date.now() - Math.max(mountedAt, lastRefreshAttemptAt);
    if (elapsed < ACTIVITY_REFRESH_THROTTLE_MS) {
      scheduleRefresh();
      return;
    }

    clearScheduledRefresh();

    const request = requestSessionRefresh();
    inFlight = request;
    void request.finally(() => {
      if (inFlight === request) {
        inFlight = null;
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

    const elapsed = Date.now() - Math.max(mountedAt, lastRefreshAttemptAt);
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

  document.addEventListener("visibilitychange", handleVisibilityChange);
  window.addEventListener("focus", refreshOnActivity);
  scheduleRefresh();

  return () => {
    mounted = false;
    clearScheduledRefresh();
    document.removeEventListener("visibilitychange", handleVisibilityChange);
    window.removeEventListener("focus", refreshOnActivity);
  };
}
