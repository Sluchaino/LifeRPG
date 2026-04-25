import { Link, Route, Routes } from "react-router-dom";
import { useAuth } from "./shared/auth";
import HomePage from "./pages/HomePage";
import LoginPage from "./pages/LoginPage";
import RegisterPage from "./pages/RegisterPage";
import ProfilePage from "./pages/ProfilePage";
import SkillsPage from "./pages/SkillsPage";
import CalendarPage from "./pages/CalendarPage";
import TasksPage from "./pages/TasksPage";
import HabitsPage from "./pages/HabitsPage";
import NotFoundPage from "./pages/NotFoundPage";

export default function App() {
  const { user, loading, logout } = useAuth();

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">LifeRPG</span>
          <span className="brand-tag">Реальная жизнь. Реальные показатели.</span>
        </div>
        <nav className="nav-links">
          <Link to="/">Главная</Link>
          <Link to="/profile">Профиль</Link>
          <Link to="/skills">Навыки</Link>
          <Link to="/habits">Привычки</Link>
          <Link to="/tasks">Список дел</Link>
          <Link to="/calendar">Календарь</Link>
        </nav>
        <div className="auth-zone">
          {loading ? (
            <span className="pill muted">Загрузка</span>
          ) : user ? (
            <>
              <span className="pill">Вы вошли как {user.login}</span>
              <button className="ghost" type="button" onClick={() => void logout()}>
                Выйти
              </button>
            </>
          ) : (
            <>
              <Link className="ghost" to="/login">
                Войти
              </Link>
              <Link className="primary" to="/register">
                Регистрация
              </Link>
            </>
          )}
        </div>
      </header>

      <main className="content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/skills" element={<SkillsPage />} />
          <Route path="/habits" element={<HabitsPage />} />
          <Route path="/tasks" element={<TasksPage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
    </div>
  );
}
