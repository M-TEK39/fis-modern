import { Skeleton } from "@/components/ui/skeleton";

export function RouteLoading() {
  return (
    <div className="w-full space-y-6" role="status" aria-label="Loading page" aria-busy="true">
      <span className="sr-only">Loading page…</span>
      <div className="space-y-3">
        <Skeleton className="h-7 w-64 max-w-full" />
        <Skeleton className="h-4 w-96 max-w-full" />
      </div>
      <div className="space-y-3">
        <Skeleton className="h-12 w-full rounded-lg" />
        <Skeleton className="h-12 w-full rounded-lg" />
        <Skeleton className="h-12 w-full rounded-lg" />
      </div>
    </div>
  );
}

export default RouteLoading;
