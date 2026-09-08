"use client";

type ConfirmSubmitButtonProps = {
  label: string;
  pendingLabel: string;
};

export default function ConfirmSubmitButton({ label, pendingLabel }: Readonly<ConfirmSubmitButtonProps>) {
  return <button className="button button-danger" onClick={(event) => { if (!window.confirm("This will DELETE all Trips without routes. Click OK to continue or Cancel to discontinue.")) event.preventDefault(); }} type="submit">{label || pendingLabel}</button>;
}
