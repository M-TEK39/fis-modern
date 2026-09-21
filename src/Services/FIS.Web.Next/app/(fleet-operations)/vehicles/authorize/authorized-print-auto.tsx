"use client";

import { useEffect } from "react";

export default function AuthorizedPrintAuto() {
  useEffect(() => {
    window.print();
  }, []);

  return null;
}
