"use client";

export default function PrintButton({ label = "Print this letter" }: { label?: string }) {
  return (
    <button className="button button-primary" type="button" onClick={() => window.print()}>
      {label}
    </button>
  );
}
