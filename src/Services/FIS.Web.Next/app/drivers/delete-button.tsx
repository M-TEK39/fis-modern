"use client";

type DeleteAction = (formData: FormData) => Promise<void>;

type DeleteButtonProps = {
  action: DeleteAction;
  id: number;
  name: string;
  departmentCode: number;
  siteCode: number;
  fieldName: "authoriserCode" | "siteDriverCode";
};

export default function DeleteButton({ action, id, name, departmentCode, siteCode, fieldName }: Readonly<DeleteButtonProps>) {
  return (
    <form action={action}>
      <input name={fieldName} type="hidden" value={id} />
      <input name="departmentCode" type="hidden" value={departmentCode} />
      <input name="siteCode" type="hidden" value={siteCode} />
      <button
        aria-label={`Delete ${name}`}
        className="button button-danger button-small"
        onClick={(event) => {
          if (!window.confirm(`Delete ${name}?`)) {
            event.preventDefault();
          }
        }}
        type="submit"
      >
        Delete
      </button>
    </form>
  );
}
