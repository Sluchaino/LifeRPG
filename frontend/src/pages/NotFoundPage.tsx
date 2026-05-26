import { Link } from "react-router-dom";

export default function NotFoundPage() {
  return (
    <section className="card">
      <h2>Страница не найдена</h2>
      <p className="muted">Такой страницы не существует.</p>
      <Link className="ghost" to="/">
        На главную
      </Link>
    </section>
  );
}
