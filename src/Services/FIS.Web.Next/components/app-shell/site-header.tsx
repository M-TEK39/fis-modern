"use client";

import { useEffect, useMemo, useState, useTransition } from "react";
import {
  Bell,
  CheckCheck,
  Command as CommandIcon,
  Inbox,
  LoaderCircle,
  Monitor,
  Moon,
  RefreshCw,
  Search,
  Sun,
} from "lucide-react";
import { useTheme } from "next-themes";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandShortcut,
} from "@/components/ui/command";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { ScrollArea } from "@/components/ui/scroll-area";
import { SidebarTrigger } from "@/components/ui/sidebar/sidebar";
import { Separator } from "@/components/ui/separator";
import {
  Breadcrumb,
  BreadcrumbList,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbSeparator,
  BreadcrumbPage,
} from "@/components/ui/breadcrumb";
import { getUserMessagesAction, markUserMessageReadAction } from "@/app/_actions/user-messages";
import type { UserMessage, UserMessageInbox } from "@/lib/api/administration/api-user-messages";

type NavigationItem = Readonly<{
  label: string;
  href: string;
}>;

type NavigationGroup = Readonly<{
  label: string;
  items: readonly NavigationItem[];
}>;

type ThemeMode = "light" | "dark" | "system";

function ThemeModeIcon({ mode }: Readonly<{ mode: ThemeMode }>) {
  if (mode === "light") return <Sun aria-hidden="true" />;
  if (mode === "dark") return <Moon aria-hidden="true" />;
  return <Monitor aria-hidden="true" />;
}

function HeaderSearch({ groups }: Readonly<{ groups: readonly NavigationGroup[] }>) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const router = useRouter();

  const commandGroups = useMemo(() => groups.filter((group) => group.items.length > 0), [groups]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target;
      const editing =
        target instanceof HTMLElement &&
        (target.isContentEditable ||
          target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT");

      if (editing) return;

      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setOpen(true);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  function navigate(href: string) {
    setOpen(false);
    router.push(href);
  }

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => setOpen(true)}
        className="hidden min-w-52 justify-start gap-2 text-muted-foreground md:inline-flex lg:min-w-72"
        aria-label="Search available modules"
      >
        <Search aria-hidden="true" />
        <span>Search modules...</span>
        <CommandShortcut className="ml-auto normal-case tracking-normal">Ctrl K</CommandShortcut>
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={() => setOpen(true)}
        className="md:hidden"
        aria-label="Search available modules"
      >
        <Search aria-hidden="true" />
      </Button>
      <CommandDialog open={open} onOpenChange={setOpen}>
        <CommandInput placeholder="Search the modules available to you..." />
        <CommandList>
          <CommandEmpty>No available module matches that search.</CommandEmpty>
          {commandGroups.map((group) => (
            <CommandGroup key={group.label} heading={group.label}>
              {group.items.map((item) => (
                <CommandItem
                  key={item.href}
                  value={`${group.label} ${item.label}`}
                  onSelect={() => navigate(item.href)}
                  aria-current={pathname === item.href ? "page" : undefined}
                >
                  <CommandIcon aria-hidden="true" />
                  <span>{item.label}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          ))}
        </CommandList>
      </CommandDialog>
    </>
  );
}

function HeaderThemeMenu() {
  const { setTheme, theme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  // next-themes reads the persisted preference in the browser. Keep the first
  // client render on the same system icon/value as SSR, then reveal that
  // preference after hydration to avoid mismatching the Radix trigger markup.
  const activeTheme: ThemeMode =
    mounted && (theme === "light" || theme === "dark") ? theme : "system";

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="ghost" size="icon" aria-label="Choose colour mode">
          <ThemeModeIcon mode={activeTheme} />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-40">
        <DropdownMenuRadioGroup value={activeTheme} onValueChange={(mode) => setTheme(mode)}>
          <DropdownMenuRadioItem value="light">
            <Sun aria-hidden="true" />
            Light
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="dark">
            <Moon aria-hidden="true" />
            Dark
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="system">
            <Monitor aria-hidden="true" />
            System
          </DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function HeaderNotifications({
  noticeManagementHref,
}: Readonly<{ noticeManagementHref?: string }>) {
  const [open, setOpen] = useState(false);
  const [inbox, setInbox] = useState<UserMessageInbox | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  function loadInbox() {
    setError(null);
    startTransition(async () => {
      const result = await getUserMessagesAction();
      if (!result.ok) {
        setError(result.message);
        return;
      }

      setInbox(result.data);
    });
  }

  function handleOpenChange(nextOpen: boolean) {
    setOpen(nextOpen);
    if (nextOpen) {
      loadInbox();
    }
  }

  function markRead(message: UserMessage) {
    if (message.isRead || isPending) return;

    setError(null);
    startTransition(async () => {
      const result = await markUserMessageReadAction(message.id);
      if (!result.ok) {
        setError(result.message);
        return;
      }

      setInbox((current) => {
        if (!current) return current;

        return {
          unreadCount: Math.max(0, current.unreadCount - (message.isRead ? 0 : 1)),
          items: current.items.map((item) => (item.id === result.data.id ? result.data : item)),
        };
      });
    });
  }

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={
            inbox?.unreadCount
              ? `Open notifications: ${inbox.unreadCount} unread`
              : "Open notifications"
          }
        >
          <Bell aria-hidden="true" />
          {inbox?.unreadCount ? (
            <span
              aria-hidden="true"
              className="absolute right-0.5 top-0.5 flex size-4 items-center justify-center rounded-full bg-primary text-[10px] font-semibold text-primary-foreground"
            >
              {inbox.unreadCount > 9 ? "9+" : inbox.unreadCount}
            </span>
          ) : null}
        </Button>
      </SheetTrigger>
      <SheetContent side="right" className="flex h-dvh w-full flex-col gap-0 p-0 sm:max-w-md">
        <SheetHeader className="shrink-0 border-b px-6 pb-4 pt-6">
          <SheetTitle>Notifications</SheetTitle>
          <SheetDescription>
            Messages sent specifically to your Fleet Information System account.
          </SheetDescription>
        </SheetHeader>
        <div className="min-h-0 flex-1">
          {isPending && !inbox ? (
            <div className="flex h-full flex-col items-center justify-center gap-3 px-6 text-center text-sm text-muted-foreground">
              <LoaderCircle className="size-5 animate-spin" aria-hidden="true" />
              Loading your notifications…
            </div>
          ) : error && !inbox ? (
            <div className="flex h-full flex-col items-center justify-center gap-3 px-6 text-center">
              <div className="rounded-full bg-muted p-3 text-muted-foreground">
                <Inbox aria-hidden="true" />
              </div>
              <div className="space-y-1">
                <p className="font-medium text-foreground">Notifications are unavailable</p>
                <p className="text-sm text-muted-foreground">{error}</p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={loadInbox}
                disabled={isPending}
              >
                <RefreshCw className={isPending ? "animate-spin" : undefined} aria-hidden="true" />
                Try again
              </Button>
            </div>
          ) : inbox?.items.length === 0 ? (
            <div className="flex h-full flex-col items-center justify-center gap-3 px-6 text-center">
              <div className="rounded-full bg-muted p-3 text-muted-foreground">
                <CheckCheck aria-hidden="true" />
              </div>
              <div className="space-y-1">
                <p className="font-medium text-foreground">You&apos;re all caught up</p>
                <p className="text-sm text-muted-foreground">
                  There are no notifications for you yet.
                </p>
              </div>
            </div>
          ) : inbox ? (
            <ScrollArea className="h-full">
              <div className="space-y-2 p-4">
                {error ? (
                  <p
                    role="status"
                    className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive"
                  >
                    {error}
                  </p>
                ) : null}
                {inbox.items.map((message) => (
                  <button
                    key={message.id}
                    type="button"
                    onClick={() => markRead(message)}
                    disabled={message.isRead || isPending}
                    className={`w-full rounded-lg border p-4 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-default ${
                      message.isRead
                        ? "bg-background text-muted-foreground"
                        : "border-primary/40 bg-primary/5 text-foreground hover:bg-primary/10"
                    }`}
                    aria-label={message.isRead ? "Read notification" : "Mark notification as read"}
                  >
                    <div className="flex items-start gap-3">
                      <div className="min-w-0 flex-1 space-y-2">
                        <p className="whitespace-pre-wrap text-sm leading-6">
                          {message.message || "(No message text)"}
                        </p>
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                          {message.createdAt ? (
                            <span>{formatNotificationDate(message.createdAt)}</span>
                          ) : null}
                          {!message.isRead ? (
                            <Badge variant="secondary">Unread</Badge>
                          ) : (
                            <span>Read</span>
                          )}
                        </div>
                      </div>
                      {!message.isRead ? (
                        <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" />
                      ) : null}
                    </div>
                  </button>
                ))}
              </div>
            </ScrollArea>
          ) : null}
        </div>
        <div className="flex shrink-0 items-center justify-between gap-3 border-t px-6 py-4">
          <p className="text-sm text-muted-foreground">
            {inbox?.unreadCount ? `${inbox.unreadCount} unread` : "Inbox"}
          </p>
          <div className="flex items-center gap-2">
            {noticeManagementHref ? (
              <Button asChild variant="outline" size="sm">
                <Link href={noticeManagementHref}>Scheduled notices</Link>
              </Button>
            ) : null}
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={loadInbox}
              disabled={isPending}
              aria-label="Refresh notifications"
            >
              <RefreshCw className={isPending ? "animate-spin" : undefined} aria-hidden="true" />
            </Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  );
}

function formatNotificationDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

export default function SiteHeader({ groups }: Readonly<{ groups: readonly NavigationGroup[] }>) {
  const pathname = usePathname();
  const noticeManagementHref = groups
    .flatMap((group) => group.items)
    .find((item) => item.href === "/notice-management")?.href;
  const section = (pathname.split("/").filter(Boolean)[0] ?? "home")
    .replace(/-/g, " ")
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
  return (
    <header className="sticky top-0 z-20 flex h-[--header-height] shrink-0 items-center gap-2 border-b bg-background">
      <div className="flex w-full items-center gap-2 px-4">
        <SidebarTrigger aria-label="Toggle navigation" />
        <Separator orientation="vertical" className="mx-2 h-4" />
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem className="hidden sm:block">
              <BreadcrumbLink href="/home">Fleet Information System</BreadcrumbLink>
            </BreadcrumbItem>
            <BreadcrumbSeparator className="hidden sm:block" />
            <BreadcrumbItem>
              <BreadcrumbPage>{section}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
        <div className="ml-auto flex items-center gap-1">
          <HeaderSearch groups={groups} />
          <HeaderThemeMenu />
          <HeaderNotifications noticeManagementHref={noticeManagementHref} />
        </div>
      </div>
    </header>
  );
}
