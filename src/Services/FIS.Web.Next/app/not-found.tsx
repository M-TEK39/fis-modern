import Link from "next/link";

export default function NotFound() {
  return (
    <main className="page-shell" aria-labelledby="not-found-title">
      <section className="vehicle-status-card max-w-lg text-center">
        <p className="eyebrow !text-black dark:!text-white">404</p>
        <h1 id="not-found-title" className="!text-black dark:!text-white">
          Page not found
        </h1>
        <p className="!text-black opacity-80 dark:!text-white">
          The page you requested is unavailable or may have moved.
        </p>
        <div className="button-row justify-center">
          <Link className="button button-primary" href="/home">
            Return home
          </Link>
        </div>
      </section>
    </main>
  );
}
