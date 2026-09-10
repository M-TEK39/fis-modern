"use client";

import * as React from "react";
import { CheckCircle2, CircleAlert, Clock3, LogOut, RefreshCw, ShieldCheck } from "lucide-react";

import {
  getSessionManagementAction,
  revokeSessionsAction,
} from "@/app/_actions/session-management";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type {
  ManagedSession,
  SessionManagementStatus,
  SessionRevokeInput,
} from "@/lib/api/administration/api-session-management";

type Message = { text: string; tone: "success" | "error" };

type PendingRevocation = {
  input: SessionRevokeInput;
  title: string;
  description: string;
  actionLabel: string;
};

const DATE_TIME_FORMATTER = new Intl.DateTimeFormat("en-ZA", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: "UTC",
});

function formatUtc(value: string | null) {
  if (!value) return "No activity recorded";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Unavailable" : `${DATE_TIME_FORMATTER.format(date)} UTC`;
}

function formatMinutes(value: number) {
  if (value < 60) return `${value} min`;
  const hours = value / 60;
  if (hours < 24 && Number.isInteger(hours)) return `${hours} hr`;
  const days = value / (24 * 60);
  return Number.isInteger(days) ? `${days} days` : `${value} min`;
}

function StatusMessage({ message }: Readonly<{ message: Message | null }>) {
  if (!message) return null;
  const isError = message.tone === "error";
  return (
    <div
      className={`flex items-start gap-2 rounded-lg border p-3 text-sm ${
        isError
          ? "border-destructive/40 bg-destructive/5 text-destructive"
          : "border-primary/30 bg-primary/5"
      }`}
      role={isError ? "alert" : "status"}
      aria-live="polite"
    >
      {isError ? (
        <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
      ) : (
        <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
      )}
      <span>{message.text}</span>
    </div>
  );
}

function SessionAction({
  session,
  managementAvailable,
  disabled,
  onRequest,
}: Readonly<{
  session: ManagedSession;
  managementAvailable: boolean;
  disabled: boolean;
  onRequest: (pending: PendingRevocation) => void;
}>) {
  const input: SessionRevokeInput = session.isCurrent
    ? { scope: "current" }
    : { scope: "user", userAccessCode: session.userAccessCode };
  return (
    <Button
      type="button"
      size="sm"
      variant={session.isCurrent ? "destructive" : "outline"}
      disabled={!managementAvailable || disabled}
      onClick={() =>
        onRequest(
          session.isCurrent
            ? {
                input,
                title: "Revoke the current session?",
                description:
                  "This will end the session you are using now. You may need to sign in again before continuing.",
                actionLabel: "Revoke current session",
              }
            : {
                input,
                title: `Revoke sessions for ${session.userAccessCode}?`,
                description:
                  "This will revoke the durable sessions associated with this user access code. It cannot be undone from this screen.",
                actionLabel: "Revoke user sessions",
              },
        )
      }
    >
      <LogOut aria-hidden="true" />
      {session.isCurrent ? "Revoke current" : "Revoke user"}
    </Button>
  );
}

function SessionRow({
  session,
  managementAvailable,
  disabled,
  onRequest,
}: Readonly<{
  session: ManagedSession;
  managementAvailable: boolean;
  disabled: boolean;
  onRequest: (pending: PendingRevocation) => void;
}>) {
  return (
    <TableRow>
      <TableCell className="font-medium">{session.userAccessCode}</TableCell>
      <TableCell>
        <time dateTime={session.createdAtUtc}>{formatUtc(session.createdAtUtc)}</time>
      </TableCell>
      <TableCell>
        <time dateTime={session.expiresAtUtc}>{formatUtc(session.expiresAtUtc)}</time>
      </TableCell>
      <TableCell>
        {session.isCurrent ? <Badge variant="secondary">Current</Badge> : <span>Other</span>}{" "}
        <Badge variant="outline">{session.rememberMe ? "Remembered" : "Standard"}</Badge>
      </TableCell>
      <TableCell className="text-right">
        <SessionAction
          session={session}
          managementAvailable={managementAvailable}
          disabled={disabled}
          onRequest={onRequest}
        />
      </TableCell>
    </TableRow>
  );
}

function MobileSessionCard({
  session,
  managementAvailable,
  disabled,
  onRequest,
}: Readonly<{
  session: ManagedSession;
  managementAvailable: boolean;
  disabled: boolean;
  onRequest: (pending: PendingRevocation) => void;
}>) {
  return (
    <li className="grid gap-3 rounded-lg border p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="grid gap-1">
          <p className="font-medium">{session.userAccessCode}</p>
          <p className="text-xs text-muted-foreground">Session activity</p>
        </div>
        {session.isCurrent ? (
          <Badge variant="secondary">Current</Badge>
        ) : (
          <Badge variant="outline">Other</Badge>
        )}
      </div>
      <dl className="grid gap-2 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-muted-foreground">Issued</dt>
          <dd className="text-right">
            <time dateTime={session.createdAtUtc}>{formatUtc(session.createdAtUtc)}</time>
          </dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-muted-foreground">Expires</dt>
          <dd className="text-right">
            <time dateTime={session.expiresAtUtc}>{formatUtc(session.expiresAtUtc)}</time>
          </dd>
        </div>
      </dl>
      <p className="text-xs text-muted-foreground">
        {session.rememberMe ? "Remember-me session" : "Standard session"}
      </p>
      <SessionAction
        session={session}
        managementAvailable={managementAvailable}
        disabled={disabled}
        onRequest={onRequest}
      />
    </li>
  );
}

export function SessionManagementPanel() {
  const [status, setStatus] = React.useState<SessionManagementStatus | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [message, setMessage] = React.useState<Message | null>(null);
  const [pending, setPending] = React.useState<PendingRevocation | null>(null);
  const [revoking, startRevoking] = React.useTransition();

  const refresh = React.useCallback(async () => {
    setLoadError(null);
    const result = await getSessionManagementAction();
    if (!result.ok) {
      setLoadError(result.message);
      return;
    }
    setStatus(result.status);
  }, []);

  React.useEffect(() => {
    void refresh();
  }, [refresh]);

  const revoke = () => {
    if (!pending) return;
    const request = pending.input;
    startRevoking(async () => {
      const result = await revokeSessionsAction(request);
      setMessage({
        text: result.message ?? (result.ok ? "Sessions revoked." : "Session revocation failed."),
        tone: result.ok ? "success" : "error",
      });
      setPending(null);
      if (result.ok) await refresh();
    });
  };

  if (loadError) {
    return (
      <div
        className="grid gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm"
        role="alert"
      >
        <div className="flex items-start gap-2 text-destructive">
          <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>{loadError}</p>
        </div>
        <Button className="w-fit" size="sm" variant="outline" onClick={() => void refresh()}>
          <RefreshCw aria-hidden="true" />
          Retry
        </Button>
      </div>
    );
  }

  if (!status) {
    return (
      <div
        className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground"
        role="status"
      >
        Loading secure session management…
      </div>
    );
  }

  const managementAvailable = status.durableSessionManagementAvailable;

  return (
    <div className="grid gap-5">
      <div className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-4">
        <div className="flex min-w-0 items-start gap-3">
          <ShieldCheck className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden="true" />
          <div className="grid gap-1">
            <p className="text-sm font-medium">Session safety</p>
            <p className="text-xs text-muted-foreground">{status.description}</p>
          </div>
        </div>
        <Badge className="shrink-0" variant={managementAvailable ? "secondary" : "outline"}>
          {managementAvailable ? "Management available" : "Read-only fallback"}
        </Badge>
      </div>

      {!managementAvailable ? (
        <div className="flex items-start gap-2 rounded-lg border border-primary/30 bg-muted/50 p-3 text-sm">
          <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>
            Durable session management is unavailable. This database does not expose an active
            session inventory or cross-server revocation until the optional durable session store is
            available.
          </p>
        </div>
      ) : null}

      <StatusMessage message={message} />

      <div className="grid gap-3 sm:grid-cols-3">
        <Card>
          <CardHeader className="p-4 pb-2">
            <CardDescription>Access token lifetime</CardDescription>
            <CardTitle className="text-2xl">
              {formatMinutes(status.accessTokenLifetimeMinutes)}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="p-4 pb-2">
            <CardDescription>Standard refresh</CardDescription>
            <CardTitle className="text-2xl">
              {formatMinutes(status.standardRefreshLifetimeMinutes)}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="p-4 pb-2">
            <CardDescription>Remember-me refresh</CardDescription>
            <CardTitle className="text-2xl">
              {formatMinutes(status.rememberRefreshLifetimeMinutes)}
            </CardTitle>
          </CardHeader>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="grid gap-1">
              <CardTitle>Active sessions</CardTitle>
              <CardDescription>
                Session identifiers and raw access or refresh tokens are not displayed. Revoke a
                current session or all sessions for a user after confirming the consequence.
              </CardDescription>
            </div>
            <Button
              type="button"
              variant="destructive"
              disabled={!managementAvailable || revoking || status.sessions.length === 0}
              onClick={() =>
                setPending({
                  input: { scope: "all" },
                  title: "Revoke all sessions?",
                  description:
                    "This will revoke every durable session returned by the session manager, including the current session. Everyone affected will need to sign in again.",
                  actionLabel: "Revoke all sessions",
                })
              }
            >
              <LogOut aria-hidden="true" />
              Revoke all sessions
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {status.sessions.length === 0 ? (
            <div className="grid justify-items-center gap-2 rounded-lg border border-dashed p-8 text-center">
              <Clock3 className="size-5 text-muted-foreground" aria-hidden="true" />
              <p className="font-medium">No active sessions were returned.</p>
              <p className="text-sm text-muted-foreground">
                Refresh the list if another administrator has just signed in.
              </p>
            </div>
          ) : (
            <>
              <div className="hidden md:block">
                <Table>
                  <caption className="sr-only">Active FIS sessions</caption>
                  <TableHeader>
                    <TableRow>
                      <TableHead>User access code</TableHead>
                      <TableHead>Issued</TableHead>
                      <TableHead>Expires</TableHead>
                      <TableHead>State</TableHead>
                      <TableHead className="text-right">Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {status.sessions.map((session) => (
                      <SessionRow
                        key={`${session.userAccessCode}-${session.createdAtUtc}`}
                        session={session}
                        managementAvailable={managementAvailable}
                        disabled={revoking}
                        onRequest={setPending}
                      />
                    ))}
                  </TableBody>
                </Table>
              </div>
              <ul className="grid gap-3 md:hidden" aria-label="Active FIS sessions">
                {status.sessions.map((session) => (
                  <MobileSessionCard
                    key={`${session.userAccessCode}-${session.createdAtUtc}`}
                    session={session}
                    managementAvailable={managementAvailable}
                    disabled={revoking}
                    onRequest={setPending}
                  />
                ))}
              </ul>
            </>
          )}
        </CardContent>
      </Card>

      <AlertDialog
        open={pending !== null}
        onOpenChange={(open) => !open && !revoking && setPending(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{pending?.title ?? "Confirm session revocation"}</AlertDialogTitle>
            <AlertDialogDescription>{pending?.description}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={revoking}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={revoking}
              onClick={(event) => {
                event.preventDefault();
                revoke();
              }}
            >
              {revoking ? "Revoking…" : (pending?.actionLabel ?? "Revoke sessions")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
