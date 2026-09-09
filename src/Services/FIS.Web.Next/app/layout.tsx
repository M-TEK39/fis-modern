import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense, type ReactNode } from "react";

import AppShell from "@/app/_components/app-shell";
import { ThemeProvider } from "@/components/theme-provider";
import { getSession } from "@/lib/session";

import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Fleet Information System",
    template: "%s | Fleet Information System",
  },
  description: "Fleet Information System",
};

async function SessionShell({ children }: Readonly<{ children: ReactNode }>) {
  await connection();
  const session = await getSession();

  if (session.status !== "authenticated") {
    return children;
  }

  return <AppShell session={session}>{children}</AppShell>;
}

function SessionShellFallback() {
  return (
    <main className="flex min-h-screen items-center justify-center p-6" aria-busy="true">
      <p className="text-sm text-muted-foreground">Loading Fleet Information System...</p>
    </main>
  );
}

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <ThemeProvider>
          <Suspense fallback={<SessionShellFallback />}>
            <SessionShell>{children}</SessionShell>
          </Suspense>
        </ThemeProvider>
      </body>
    </html>
  );
}
