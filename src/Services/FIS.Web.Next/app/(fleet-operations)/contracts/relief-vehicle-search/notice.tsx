export function Notice({ message, isError }: Readonly<{ message: string; isError: boolean }>) {
  return (
    <div
      className={isError ? "notice notice-error" : "notice notice-success"}
      role={isError ? "alert" : "status"}
    >
      {message}
    </div>
  );
}
