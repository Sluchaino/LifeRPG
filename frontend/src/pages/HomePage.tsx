import { Link } from "react-router-dom";
import { useAuth } from "../shared/auth";

export default function HomePage() {
  const { user } = useAuth();

  return (
    <section className="hero">
      <div className="hero-card">
        <p className="eyebrow">Ядро LifeRPG</p>
        <h1>Преврати привычки в рост персонажа.</h1>
        <p className="subline">
          Отслеживай прогресс в реальной жизни, развивай характеристики и навыки
          через понятный RPG‑цикл. Начни с регистрации, профиля и собственных навыков.
        </p>
        <div className="hero-actions">
          {user ? (
            <>
              <Link className="primary" to="/profile">
                Открыть профиль
              </Link>
              <Link className="ghost" to="/skills">
                Управлять навыками
              </Link>
            </>
          ) : (
            <>
              <Link className="primary" to="/register">
                Регистрация
              </Link>
              <Link className="ghost" to="/login">
                Войти
              </Link>
            </>
          )}
        </div>
      </div>
      <div className="hero-panel">
        <div className="panel-item">
          <span className="panel-title">Характеристики</span>
          <span className="panel-value">Сила, Мудрость, Дисциплина</span>
        </div>
        <div className="panel-item">
          <span className="panel-title">Навыки</span>
          <span className="panel-value">Личные, гибкие, настраиваемые</span>
        </div>
        <div className="panel-item">
          <span className="panel-title">Авторизация</span>
          <span className="panel-value">На cookies, без JWT</span>
        </div>
      </div>
    </section>
  );
}
