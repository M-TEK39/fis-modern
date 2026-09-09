import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense, type ReactNode } from "react";

import AppShell from "@/app/_components/app-shell";
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

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <Suspense fallback={children}>
          <SessionShell>{children}</SessionShell>
        </Suspense>
      </body>
    </html>
  );
}
