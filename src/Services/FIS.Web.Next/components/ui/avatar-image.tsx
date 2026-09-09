import Image, { type ImageProps } from "next/image";

import { cn } from "../lib/utils";

function AvatarImage({ className, alt, sizes = "2.5rem", ...props }: Omit<ImageProps, "fill">) {
  return (
    <Image
      alt={alt}
      fill
      sizes={sizes}
      className={cn("aspect-square h-full w-full object-cover", className)}
      {...props}
    />
  );
}

export { AvatarImage };
