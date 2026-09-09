"use client";

import * as React from "react";
import { MinusIcon } from "@radix-ui/react-icons";

function InputOTPSeparator({
  ref,
  ...props
}: React.ComponentPropsWithoutRef<"div"> & { ref?: React.Ref<React.ElementRef<"div">> }) {
  return (
    <div ref={ref} aria-hidden="true" {...props}>
      <MinusIcon />
    </div>
  );
}
InputOTPSeparator.displayName = "InputOTPSeparator";

export { InputOTPSeparator };
