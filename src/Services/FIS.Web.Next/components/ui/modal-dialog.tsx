"use client";

import { useEffect, useRef, type ReactNode } from "react";

type ModalDialogProps = Readonly<{
  open: boolean;
  onClose: () => void;
  labelledBy: string;
  className?: string;
  children: ReactNode;
}>;

export function ModalDialog({ open, onClose, labelledBy, className, children }: ModalDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      className={className ? `modal-dialog ${className}` : "modal-dialog"}
      aria-labelledby={labelledBy}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClose={() => onClose()}
    >
      {children}
    </dialog>
  );
}
