import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";

type HabitResponse = {
  id: string;
  name: string;
  description?: string | null;
  isCompletedToday: boolean;
  completedDays: number;
  lastCompletedDate?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

type HabitsResponse = {
  date: string;
  discipline: number;
  totalHabits: number;
  completedHabits: number;
  habits: HabitResponse[];
};

export default function HabitsPage() {
  const { user, loading } = useAuth();
  const [data, setData] = useState<HabitsResponse | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [editingHabitId, setEditingHabitId] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const loadHabits = useCallback(async () => {
    if (!user) {
      setData(null);
      return;
    }

    try {
      const response = await api.get<HabitsResponse>("/habits");
      setData({
        ...response,
        habits: [...response.habits].sort((a, b) => a.name.localeCompare(b.name, "ru"))
      });
      setError(null);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось загрузить привычки.");
      }
    }
  }, [user]);

  useEffect(() => {
    if (!user) {
      setData(null);
      return;
    }

    void loadHabits();
  }, [user, loadHabits]);

  const canSubmit = useMemo(() => name.trim().length >= 2 && !busy, [name, busy]);

  const resetForm = () => {
    setName("");
    setDescription("");
    setEditingHabitId(null);
  };

  const openCreateModal = () => {
    resetForm();
    setIsModalOpen(true);
    setError(null);
  };

  const openEditModal = (habit: HabitResponse) => {
    setEditingHabitId(habit.id);
    setName(habit.name);
    setDescription(habit.description ?? "");
    setIsModalOpen(true);
    setError(null);
  };

  const closeModal = () => {
    setIsModalOpen(false);
    resetForm();
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) {
      return;
    }

    setBusy(true);
    setError(null);

    try {
      const payload = {
        name,
        description: description.trim() ? description.trim() : null
      };

      if (editingHabitId) {
        await api.patch(`/habits/${editingHabitId}`, payload);
      } else {
        await api.post("/habits", payload);
      }

      closeModal();
      await loadHabits();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError(
          editingHabitId
            ? "Не удалось обновить привычку."
            : "Не удалось добавить привычку."
        );
      }
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (habitId: string) => {
    if (!window.confirm("Удалить привычку?")) {
      return;
    }

    setError(null);
    try {
      await api.delete(`/habits/${habitId}`);
      await loadHabits();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось удалить привычку.");
      }
    }
  };

  const toggleCompletion = async (habit: HabitResponse) => {
    if (!data) {
      return;
    }

    setError(null);
    try {
      await api.post(`/habits/${habit.id}/toggle-completion`, {
        isCompleted: !habit.isCompletedToday,
        date: data.date
      });
      await loadHabits();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось обновить выполнение привычки.");
      }
    }
  };

  if (loading) {
    return <div className="card">Загрузка привычек...</div>;
  }

  if (!user) {
    return (
      <div className="card">
        <h2>Привычки</h2>
        <p className="muted">Войдите, чтобы управлять ежедневными привычками.</p>
      </div>
    );
  }

  return (
    <section className="card">
      <div className="skills-header">
        <div>
          <h2>Привычки</h2>
          <p className="muted">
            Дисциплина теперь растёт только через ежедневное выполнение привычек.
          </p>
        </div>
        <button className="primary" type="button" onClick={openCreateModal}>
          Добавить привычку
        </button>
      </div>

      {error && <div className="error">{error}</div>}

      {data && (
        <div className="stats-row">
          <div className="stat-box">
            <span className="stat-label">Дисциплина</span>
            <span className="stat-number">{data.discipline}</span>
          </div>
          <div className="stat-box">
            <span className="stat-label">Всего привычек</span>
            <span className="stat-number">{data.totalHabits}</span>
          </div>
          <div className="stat-box">
            <span className="stat-label">Выполнено сегодня</span>
            <span className="stat-number">
              {data.completedHabits}/{data.totalHabits}
            </span>
          </div>
          <div className="stat-box">
            <span className="stat-label">Текущая дата</span>
            <span className="stat-number">{data.date}</span>
          </div>
        </div>
      )}

      {!data || data.habits.length === 0 ? (
        <div className="muted">Пока нет привычек. Добавьте первую.</div>
      ) : (
        <div className="list">
          {data.habits.map((habit) => (
            <div key={habit.id} className={`list-item ${habit.isCompletedToday ? "is-completed" : ""}`}>
              <div className="task-row">
                <label className="task-check">
                  <input
                    type="checkbox"
                    checked={habit.isCompletedToday}
                    aria-label="Отметить выполнение привычки"
                    onChange={() => void toggleCompletion(habit)}
                  />
                </label>
                <div className="task-main">
                  <div className="list-title">{habit.name}</div>
                  <div className="list-meta">
                    {habit.description?.trim() || "Без описания"}
                  </div>
                  <div className="list-tags">
                    <span className="pill">Выполнено дней: {habit.completedDays}</span>
                    {habit.lastCompletedDate && (
                      <span className="pill">Последний раз: {habit.lastCompletedDate}</span>
                    )}
                  </div>
                </div>
              </div>
              <div className="list-actions">
                <button className="ghost" type="button" onClick={() => openEditModal(habit)}>
                  Редактировать
                </button>
                <button
                  className="ghost danger"
                  type="button"
                  onClick={() => void handleDelete(habit.id)}
                >
                  Удалить
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {isModalOpen && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-header">
              <h3>{editingHabitId ? "Редактирование привычки" : "Новая привычка"}</h3>
              <button className="ghost" type="button" onClick={closeModal}>
                Закрыть
              </button>
            </div>
            <form className="form" onSubmit={handleSubmit}>
              <label className="field">
                <span>Название</span>
                <input
                  type="text"
                  placeholder="Например: 30 минут чтения"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  required
                />
              </label>
              <label className="field">
                <span>Описание</span>
                <input
                  type="text"
                  placeholder="Необязательно"
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                />
              </label>
              {error && <div className="error">{error}</div>}
              <div className="button-row">
                <button className="primary" type="submit" disabled={!canSubmit}>
                  {busy
                    ? "Сохраняем..."
                    : editingHabitId
                      ? "Сохранить"
                      : "Добавить привычку"}
                </button>
                <button className="ghost" type="button" onClick={closeModal}>
                  Отмена
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  );
}
