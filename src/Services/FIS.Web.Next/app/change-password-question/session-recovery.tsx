"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { AuthNotice } from "@/app/_components/auth-ui";
import { Button } from "@/components/ui/button";

export default function ChangePasswordQuestionSessionRecovery({
  returnPath,
}: Readonly<{ returnPath: string }>) {
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
    <div className="flex flex-col gap-6" aria-live="polite">
      {failed ? (
        <AuthNotice tone="error">
          Your session could not be refreshed. Please try again or sign in again.
        </AuthNotice>
      ) : null}
      <div
        className="flex flex-col items-center gap-3 text-center text-sm text-muted-foreground"
        aria-busy={pending}
      >
        {pending ? (
          <span
            className="h-4 w-4 animate-spin rounded-full border-2 border-muted-foreground/30 border-t-muted-foreground"
            aria-hidden="true"
          />
        ) : null}
        <p>{pending ? "Refreshing your session..." : "Your session has expired."}</p>
      </div>
      <div className="flex flex-col gap-3">
        <Button
          className="w-full"
          type="button"
          onClick={() => void refreshSession()}
          disabled={pending}
        >
          {pending ? "Retrying..." : "Try again"}
        </Button>
        <Button asChild className="w-full" variant="outline">
          <Link href="/login">Return to sign in</Link>
        </Button>
      </div>
    </div>
  );
}
