import * as React from "react";

import { cn } from "../lib/utils";

function AvatarFallback({ className, ...props }: React.ComponentProps<"span">) {
  return (
    <span
      className={cn(
        "bg-muted flex h-full w-full items-center justify-center rounded-full",
        className,
      )}
      {...props}
    />
  );
}

export { AvatarFallback };
