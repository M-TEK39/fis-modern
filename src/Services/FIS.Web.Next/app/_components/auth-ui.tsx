import Image from "next/image";
import Link from "next/link";
import type { ReactNode } from "react";

export function AuthBrand({ caption = "Gauteng Provincial Government" }: { caption?: string }) {
  return (
    <Link className="mb-8 flex flex-col items-center gap-2 text-center" href="/login">
      <span className="flex h-10 w-10 items-center justify-center overflow-hidden rounded-md bg-white p-0.5 ring-1 ring-border">
        <Image
          src="/logo/gauteng-g-fleet.webp"
          alt=""
          width={40}
          height={40}
          className="size-full object-contain"
          priority
        />
      </span>
      <span className="text-xl font-bold leading-tight">Fleet Information System</span>
      <span className="text-sm text-muted-foreground">{caption}</span>
    </Link>
  );
}

export function AuthPage({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <main className="flex min-h-svh w-full items-center justify-center bg-background px-6 py-10">
      <div className="w-full max-w-sm">{children}</div>
    </main>
  );
}

export function AuthHeader({
  title,
  description,
  id,
}: Readonly<{ title: string; description: string; id: string }>) {
  return (
    <header className="mb-6 text-center">
      <h1 id={id} className="text-xl font-bold tracking-tight">
        {title}
      </h1>
      <p className="mt-2 text-sm text-muted-foreground">{description}</p>
    </header>
  );
}

export function AuthFooter({ children }: Readonly<{ children: ReactNode }>) {
  return <footer className="mt-6 grid gap-2 text-center">{children}</footer>;
}

export function AuthNotice({
  children,
  tone = "info",
}: Readonly<{ children: ReactNode; tone?: "info" | "error" | "success" | "warning" }>) {
  const toneClassName =
    tone === "error"
      ? "border-destructive/40 bg-destructive/10 text-destructive"
      : tone === "success"
        ? "border-green-600/40 bg-green-600/10 text-green-700 dark:text-green-400"
        : tone === "warning"
          ? "border-ring/50 bg-ring/10 text-foreground"
          : "border-border bg-muted text-muted-foreground";

  return (
    <div
      className={`mb-6 rounded-md border px-3 py-2 text-sm ${toneClassName}`}
      role={tone === "error" ? "alert" : "status"}
    >
      {children}
    </div>
  );
}
