import { FormEvent, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";

export default function LoginPage() {
  const navigate = useNavigate();
  const { setUser } = useAuth();
  const [login, setLogin] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      const response = await api.post<{ id: string; login: string }>(
        "/auth/login",
        {
          login,
          password
        }
      );
      setUser(response);
      navigate("/profile");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось войти. Попробуйте ещё раз.");
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card auth-card">
      <h2>Вход</h2>
      <p className="muted">
        Войдите, чтобы увидеть профиль и навыки.
      </p>
      <form className="form" onSubmit={handleSubmit}>
        <label className="field">
          <span>Логин</span>
          <input
            type="text"
            placeholder="Ваш логин"
            value={login}
            onChange={(event) => setLogin(event.target.value)}
            required
          />
        </label>
        <label className="field">
          <span>Пароль</span>
          <div className="password-field">
            <input
              type={showPassword ? "text" : "password"}
              placeholder="Ваш пароль"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
            <button
              className="password-toggle"
              type="button"
              onClick={() => setShowPassword((value) => !value)}
              aria-label={showPassword ? "Скрыть пароль" : "Показать пароль"}
              aria-pressed={showPassword}
            >
              {showPassword ? (
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <path
                    d="M12 5c5.2 0 9.6 3.1 11 7-1.4 3.9-5.8 7-11 7S2.4 15.9 1 12c1.4-3.9 5.8-7 11-7Zm0 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm0 2.2A1.8 1.8 0 1 1 10.2 12 1.8 1.8 0 0 1 12 10.2Z"
                    fill="currentColor"
                  />
                </svg>
              ) : (
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <path
                    d="M3 5.27 5.28 3 21 18.73 18.73 21l-2.1-2.1A10.4 10.4 0 0 1 12 20C6.5 20 2.05 16.5 1 12c.56-2.26 2.05-4.24 4.18-5.71L3 5.27Zm6.44 6.44A2.5 2.5 0 0 0 12.3 14.6l-2.86-2.89Zm8.98 2.54c1.1-.9 1.9-2.05 2.28-3.25-.82-2.62-3.2-5-6.1-6.1-.95-.36-1.97-.55-3.03-.57l2.13 2.13A4.5 4.5 0 0 1 17.5 12c0 .76-.2 1.47-.55 2.07l1.47 1.48ZM12 7.5c-.22 0-.43.02-.64.06l1.58 1.58c.03-.21.06-.42.06-.64A1.5 1.5 0 0 0 12 7.5Z"
                    fill="currentColor"
                  />
                </svg>
              )}
            </button>
          </div>
        </label>
        {error && <div className="error">{error}</div>}
        <button className="primary" type="submit" disabled={busy}>
          {busy ? "Входим..." : "Войти"}
        </button>
      </form>
    </section>
  );
}
