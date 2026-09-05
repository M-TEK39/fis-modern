import "server-only";

import { cookies } from "next/headers";

const ACCESS_COOKIE = "FIS_Access_Token";
const REFRESH_COOKIE = "FIS_Refresh_Token";
const API_TIMEOUT_MS = 8_000;

export type ForwardedAuthCookie = {
  name: typeof ACCESS_COOKIE | typeof REFRESH_COOKIE;
  value: string;
  expires?: Date;
};

export type ApiLoginResult =
  | {
      ok: true;
      passwordExpired: boolean;
      passwordExpiresIn: number;
      cookies: ForwardedAuthCookie[];
    }
  | {
      ok: false;
      reason: "invalid-credentials" | "unavailable" | "invalid-response";
    };

export type ForgotPasswordResult =
  | { ok: true }
  | { ok: false; reason: "unavailable" | "invalid-response"; message?: string };

export type ChangePasswordResult =
  | { ok: true; message?: string }
  | {
      ok: false;
      reason: "unauthorized" | "unavailable" | "invalid-response";
      message?: string;
    };

type ApiMutationPayload = {
  success?: boolean;
  message?: string;
};

type LoginPayload = {
  token?: string;
  expiresAt?: string;
  passwordExpired?: boolean;
  passwordExpiresIn?: number;
};

function getApiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

async function fetchApi(path: string, init: RequestInit = {}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    return await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      signal: controller.signal,
    });
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T;
  } catch {
    return null;
  }
}

function getSetCookieHeaders(headers: Headers) {
  const headersWithSetCookie = headers as Headers & {
    getSetCookie?: () => string[];
  };
  const setCookies = headersWithSetCookie.getSetCookie?.();
  if (setCookies && setCookies.length > 0) {
    return setCookies;
  }

  const combined = headers.get("set-cookie");
  return combined ? combined.split(/,(?=\s*[A-Za-z0-9_]+=[^;,]*)/) : [];
}

function extractCookie(headers: Headers, name: ForwardedAuthCookie["name"]): ForwardedAuthCookie | null {
  const prefix = `${name}=`;
  for (const header of getSetCookieHeaders(headers)) {
    if (!header.startsWith(prefix)) {
      continue;
    }

    const [pair, ...attributes] = header.split(";");
    const value = pair.slice(prefix.length);
    if (!value) {
      continue;
    }

    const expiresAttribute = attributes.find((attribute) => /^\s*expires=/i.test(attribute));
    const expiresValue = expiresAttribute?.replace(/^\s*expires=/i, "").trim();
    const expires = expiresValue ? new Date(expiresValue) : undefined;

    return {
      name,
      value,
      expires: expires && !Number.isNaN(expires.getTime()) ? expires : undefined,
    };
  }

  return null;
}

function fallbackAccessCookie(payload: LoginPayload) {
  if (!payload.token || !payload.expiresAt) {
    return null;
  }

  const expires = new Date(payload.expiresAt);
  return {
    name: ACCESS_COOKIE,
    value: payload.token,
    expires: Number.isNaN(expires.getTime()) ? undefined : expires,
  } satisfies ForwardedAuthCookie;
}

export async function getForwardedAuthCookieHeader() {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(ACCESS_COOKIE)?.value;
  const refreshToken = cookieStore.get(REFRESH_COOKIE)?.value;

  return [
    accessToken ? `${ACCESS_COOKIE}=${accessToken}` : null,
    refreshToken ? `${REFRESH_COOKIE}=${refreshToken}` : null,
  ]
    .filter((value): value is string => value !== null)
    .join("; ");
}

export async function setAuthCookies(authCookies: ForwardedAuthCookie[]) {
  const cookieStore = await cookies();
  const secure = process.env.NODE_ENV === "production";

  for (const cookie of authCookies) {
    cookieStore.set({
      name: cookie.name,
      value: cookie.value,
      expires: cookie.expires,
      httpOnly: true,
      path: "/",
      sameSite: "lax",
      secure,
    });
  }
}

export async function loginAgainstApi(username: string, password: string): Promise<ApiLoginResult> {
  try {
    const loginResponse = await fetchApi("api/auth/login", {
      method: "POST",
      headers: { "content-type": "application/json", accept: "application/json" },
      body: JSON.stringify({ username, password }),
    });

    const payload = await readJson<LoginPayload>(loginResponse);
    if (!loginResponse.ok) {
      return { ok: false, reason: loginResponse.status >= 500 ? "unavailable" : "invalid-credentials" };
    }

    const authCookies = [
      extractCookie(loginResponse.headers, ACCESS_COOKIE),
      extractCookie(loginResponse.headers, REFRESH_COOKIE),
    ].filter((cookie): cookie is ForwardedAuthCookie => cookie !== null);

    if (!authCookies.some((cookie) => cookie.name === ACCESS_COOKIE)) {
      const fallbackCookie = fallbackAccessCookie(payload ?? {});
      if (fallbackCookie) {
        authCookies.push(fallbackCookie);
      }
    }

    if (!authCookies.some((cookie) => cookie.name === ACCESS_COOKIE)) {
      return { ok: false, reason: "invalid-response" };
    }

    return {
      ok: true,
      passwordExpired: payload?.passwordExpired === true,
      passwordExpiresIn: payload?.passwordExpiresIn ?? Number.MAX_SAFE_INTEGER,
      cookies: authCookies,
    };
  } catch (error) {
    console.error("FIS API login request failed", error instanceof Error ? error.message : "unknown error");
    return { ok: false, reason: "unavailable" };
  }
}

export async function startForgotPassword(identifier: string): Promise<ForgotPasswordResult> {
  try {
    const response = await fetchApi("api/auth/forgot-password/start", {
      method: "POST",
      headers: { "content-type": "application/json", accept: "application/json" },
      body: JSON.stringify({ username: identifier }),
    });

    const payload = await readJson<{ success?: boolean }>(response);
    if (!response.ok) {
      return {
        ok: false,
        reason: response.status >= 500 ? "unavailable" : "invalid-response",
        message: undefined,
      };
    }

    return payload?.success === true ? { ok: true } : { ok: false, reason: "invalid-response" };
  } catch (error) {
    console.error("FIS API forgot-password request failed", error instanceof Error ? error.message : "unknown error");
    return { ok: false, reason: "unavailable" };
  }
}

export async function confirmForgotPassword(
  token: string,
  newPassword: string,
  confirmNewPassword: string,
): Promise<ForgotPasswordResult> {
  try {
    const response = await fetchApi("api/auth/forgot-password/confirm", {
      method: "POST",
      headers: { "content-type": "application/json", accept: "application/json" },
      body: JSON.stringify({ token, newPassword, confirmNewPassword }),
    });
    const payload = await readJson<ApiMutationPayload>(response);

    if (!response.ok) {
      return {
        ok: false,
        reason: response.status >= 500 ? "unavailable" : "invalid-response",
        message: payload?.message,
      };
    }

    if (payload?.success !== true) {
      return { ok: false, reason: "invalid-response", message: payload?.message };
    }

    return { ok: true };
  } catch (error) {
    console.error("FIS API password reset request failed", error instanceof Error ? error.message : "unknown error");
    return { ok: false, reason: "unavailable" };
  }
}

export type SessionState =
  | { status: "anonymous" }
  | {
      status: "authenticated";
      email?: string;
      userAccessCode?: string;
      accessLevel?: string;
      roles: string[];
      passwordChangeRequired: boolean;
    }
  | { status: "expired" }
  | { status: "unavailable" };

export async function validateSession(): Promise<SessionState> {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(ACCESS_COOKIE)?.value;
  const refreshToken = cookieStore.get(REFRESH_COOKIE)?.value;

  if (!accessToken) {
    return refreshToken ? { status: "expired" } : { status: "anonymous" };
  }

  try {
    const response = await fetchApi("api/auth/validate", {
      headers: { cookie: `${ACCESS_COOKIE}=${accessToken}` },
    });

    if (response.status === 401 || response.status === 403) {
      return refreshToken ? { status: "expired" } : { status: "anonymous" };
    }

    if (!response.ok) {
      return { status: "unavailable" };
    }

    const payload = await readJson<{
      valid?: boolean;
      claims?: Array<{ type?: string; value?: string }>;
    }>(response);

    if (payload?.valid !== true) {
      return refreshToken ? { status: "expired" } : { status: "anonymous" };
    }

    const claims = payload.claims ?? [];
    const email = claims.find((claim) => claim.type?.endsWith("/emailaddress") || claim.type === "email")?.value;
    const userAccessCode = claims.find((claim) => claim.type === "user_access_code")?.value;
    const passwordChangeRequiredValue = claims.find((claim) => claim.type === "password_change_required")?.value;
    const passwordChangeRequired = ["true", "1", "y", "yes"].includes(
      passwordChangeRequiredValue?.toLowerCase() ?? "",
    );
    const accessLevel = claims.find((claim) => claim.type === "access_level")?.value;
    const roles = claims
      .filter(
        (claim) =>
          claim.type === "role" ||
          claim.type === "roles" ||
          claim.type?.endsWith("/role") === true,
      )
      .flatMap((claim) => claim.value?.split(",") ?? [])
      .map((role) => role.trim())
      .filter((role) => role.length > 0);

    return { status: "authenticated", email, userAccessCode, accessLevel, roles, passwordChangeRequired };
  } catch (error) {
    console.error("FIS API session validation failed", error instanceof Error ? error.message : "unknown error");
    return { status: "unavailable" };
  }
}

export async function resolveAuthenticatedUsername(): Promise<
  | { ok: true; username: string }
  | { ok: false; reason: "unauthorized" | "unavailable" }
> {
  const session = await validateSession();
  if (session.status === "unavailable") {
    return { ok: false, reason: "unavailable" };
  }

  if (session.status !== "authenticated") {
    return { ok: false, reason: "unauthorized" };
  }

  const username = session.userAccessCode?.trim() || session.email?.trim();
  return username ? { ok: true, username } : { ok: false, reason: "unauthorized" };
}

export async function changePasswordAgainstApi(
  username: string,
  currentPassword: string,
  newPassword: string,
  confirmNewPassword: string,
): Promise<ChangePasswordResult> {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    return { ok: false, reason: "unauthorized" };
  }

  try {
    const response = await fetchApi("api/auth/change-password", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        accept: "application/json",
        cookie: cookieHeader,
      },
      body: JSON.stringify({ username, currentPassword, newPassword, confirmNewPassword }),
    });
    const payload = await readJson<ApiMutationPayload>(response);

    if (!response.ok) {
      return {
        ok: false,
        reason: response.status === 401 || response.status === 403 ? "unauthorized" : response.status >= 500 ? "unavailable" : "invalid-response",
        message: payload?.message,
      };
    }

    if (payload?.success !== true) {
      return { ok: false, reason: "invalid-response", message: payload?.message };
    }

    return { ok: true, message: payload.message };
  } catch (error) {
    console.error("FIS API change-password request failed", error instanceof Error ? error.message : "unknown error");
    return { ok: false, reason: "unavailable" };
  }
}

export async function changePasswordQuestionAgainstApi(
  username: string,
  currentPassword: string,
  newPassword: string,
  confirmNewPassword: string,
  securityQuestion: string,
  securityAnswer: string,
  email: string,
): Promise<ChangePasswordResult> {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    return { ok: false, reason: "unauthorized" };
  }

  try {
    const response = await fetchApi("api/auth/change-password-question", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        accept: "application/json",
        cookie: cookieHeader,
      },
      body: JSON.stringify({
        username,
        oldPassword: currentPassword,
        newPassword,
        confirmNewPassword,
        securityQuestion,
        securityAnswer,
        email,
      }),
    });
    const payload = await readJson<ApiMutationPayload>(response);

    if (!response.ok) {
      return {
        ok: false,
        reason: response.status === 401 || response.status === 403 ? "unauthorized" : response.status >= 500 ? "unavailable" : "invalid-response",
        message: payload?.message,
      };
    }

    if (payload?.success !== true) {
      return { ok: false, reason: "invalid-response", message: payload?.message };
    }

    return { ok: true, message: payload.message };
  } catch (error) {
    console.error("FIS API change-password-question request failed", error instanceof Error ? error.message : "unknown error");
    return { ok: false, reason: "unavailable" };
  }
}

export async function refreshAgainstApi() {
  const cookieStore = await cookies();
  const refreshToken = cookieStore.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) {
    return { ok: false as const, reason: "missing-refresh-token" as const };
  }

  try {
    const response = await fetchApi("api/auth/refresh", {
      method: "POST",
      headers: { cookie: `${REFRESH_COOKIE}=${refreshToken}` },
    });
    const payload = await readJson<LoginPayload>(response);
    if (!response.ok) {
      return { ok: false as const, reason: response.status >= 500 ? "unavailable" as const : "unauthorized" as const };
    }

    const authCookies = [
      extractCookie(response.headers, ACCESS_COOKIE),
      extractCookie(response.headers, REFRESH_COOKIE),
    ].filter((cookie): cookie is ForwardedAuthCookie => cookie !== null);

    if (!authCookies.some((cookie) => cookie.name === ACCESS_COOKIE)) {
      const fallbackCookie = fallbackAccessCookie(payload ?? {});
      if (fallbackCookie) {
        authCookies.push(fallbackCookie);
      }
    }

    if (!authCookies.some((cookie) => cookie.name === ACCESS_COOKIE)) {
      return { ok: false as const, reason: "invalid-response" as const };
    }

    await setAuthCookies(authCookies);
    return { ok: true as const };
  } catch (error) {
    console.error("FIS API session refresh failed", error instanceof Error ? error.message : "unknown error");
    return { ok: false as const, reason: "unavailable" as const };
  }
}

export async function logoutAgainstApi() {
  const cookieStore = await cookies();
  const cookieHeader = await getForwardedAuthCookieHeader();

  try {
    await fetchApi("api/auth/logout", {
      method: "POST",
      headers: cookieHeader ? { cookie: cookieHeader } : undefined,
    });
  } catch (error) {
    console.error("FIS API logout request failed", error instanceof Error ? error.message : "unknown error");
  } finally {
    cookieStore.delete(ACCESS_COOKIE);
    cookieStore.delete(REFRESH_COOKIE);
  }
}
