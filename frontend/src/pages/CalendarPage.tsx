import { Link } from "react-router-dom";
import { useEffect, useMemo, useState } from "react";
import { ATTRIBUTE_OPTIONS, buildAttributeGradient } from "../shared/attributes";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";
import {
  CalendarTask,
  CalendarTaskMap,
  DIFFICULTY_LABELS,
  IMPORTANCE_LABELS,
  compareTasks,
  formatLocalDate
} from "../shared/tasks";

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

type UserSkill = {
  userSkillId: string;
  name: string;
  attributes: string[];
};

function getMonthMatrix(date: Date) {
  const year = date.getFullYear();
  const month = date.getMonth();
  const start = new Date(year, month, 1);
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const startDay = (start.getDay() + 6) % 7;

  const cells: Array<{ day: number; date: Date } | null> = [];
  for (let i = 0; i < startDay; i += 1) {
    cells.push(null);
  }

  for (let day = 1; day <= daysInMonth; day += 1) {
    cells.push({ day, date: new Date(year, month, day) });
  }

  return { year, month, cells };
}

const parseTime = (value: string): number | null => {
  const match = value.match(/^([01]\d|2[0-3]):([0-5]\d)$/);
  if (!match) {
    return null;
  }
  return Number(match[1]) * 60 + Number(match[2]);
};

const formatTimeInput = (value: string) => {
  const digits = value.replace(/\D/g, "").slice(0, 4);
  if (digits.length <= 2) {
    return digits.length === 2 ? `${digits}:` : digits;
  }
  return `${digits.slice(0, 2)}:${digits.slice(2)}`;
};

export default function CalendarPage() {
  const { user, loading } = useAuth();
  const today = useMemo(() => new Date(), []);
  const [selectedDate, setSelectedDate] = useState<Date>(today);
  const [tasks, setTasks] = useState<CalendarTask[]>([]);
  const [tasksError, setTasksError] = useState<string | null>(null);
  const [monthTasks, setMonthTasks] = useState<CalendarTaskMap>({});
  const [monthTasksError, setMonthTasksError] = useState<string | null>(null);
  const [skills, setSkills] = useState<UserSkill[]>([]);
  const [editingTask, setEditingTask] = useState<CalendarTask | null>(null);
  const [editDate, setEditDate] = useState("");
  const [editStartTime, setEditStartTime] = useState("");
  const [editEndTime, setEditEndTime] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const { year, month, cells } = getMonthMatrix(today);
  const selectedDateIso = formatLocalDate(selectedDate);
  const monthTitle = new Intl.DateTimeFormat("ru-RU", {
    month: "long",
    year: "numeric"
  }).format(new Date(year, month, 1));
  const selectedLabel = new Intl.DateTimeFormat("ru-RU", {
    day: "numeric",
    month: "long",
    year: "numeric"
  }).format(selectedDate);

  const skillNameById = new Map(skills.map((skill) => [skill.userSkillId, skill.name]));
  const skillGradientById = new Map(
    skills.map((skill) => [skill.userSkillId, buildAttributeGradient(skill.attributes)])
  );

  const tasksWithTime = tasks.filter((task) => task.startTime && task.endTime);
  const tasksWithoutTime = tasks.filter((task) => !task.startTime || !task.endTime);

  useEffect(() => {
    if (editingTask) {
      document.body.classList.add("modal-open");
    } else {
      document.body.classList.remove("modal-open");
    }
    return () => {
      document.body.classList.remove("modal-open");
    };
  }, [editingTask]);

  useEffect(() => {
    if (!user) {
      setTasks([]);
      return;
    }

    const loadTasks = async () => {
      setTasksError(null);
      try {
        const response = await api.get<CalendarTask[]>(`/calendar/tasks?date=${selectedDateIso}`);
        setTasks(response.sort(compareTasks));
      } catch (err) {
        if (err instanceof ApiError) {
          setTasksError(err.message);
        } else {
          setTasksError("Не удалось загрузить дела на выбранный день.");
        }
      }
    };

    void loadTasks();
  }, [user, selectedDateIso]);

  useEffect(() => {
    if (!user) {
      setMonthTasks({});
      return;
    }

    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const from = formatLocalDate(firstDay);
    const to = formatLocalDate(lastDay);

    const loadMonthTasks = async () => {
      setMonthTasksError(null);
      try {
        const response = await api.get<CalendarTask[]>(`/calendar/tasks/range?from=${from}&to=${to}`);
        const grouped: CalendarTaskMap = {};
        response.forEach((task) => {
          if (!grouped[task.date]) {
            grouped[task.date] = [];
          }
          grouped[task.date].push(task);
        });
        Object.values(grouped).forEach((items) => items.sort(compareTasks));
        setMonthTasks(grouped);
      } catch (err) {
        if (err instanceof ApiError) {
          setMonthTasksError(err.message);
        } else {
          setMonthTasksError("Не удалось загрузить календарь.");
        }
      }
    };

    void loadMonthTasks();
  }, [user, year, month]);

  useEffect(() => {
    if (!user) {
      setSkills([]);
      return;
    }

    api
      .get<UserSkill[]>("/skills")
      .then((response) => setSkills(response))
      .catch(() => setSkills([]));
  }, [user]);

  const removeTaskFromMap = (current: CalendarTaskMap, dateIso: string, taskId: string) => {
    const next = { ...current };
    if (!next[dateIso]) {
      return next;
    }
    next[dateIso] = next[dateIso].filter((task) => task.id !== taskId);
    if (next[dateIso].length === 0) {
      delete next[dateIso];
    }
    return next;
  };

  const isDateInCurrentMonth = (dateIso: string) => {
    const [yearPart, monthPart] = dateIso.split("-");
    return Number(yearPart) === year && Number(monthPart) === month + 1;
  };

  const applyTaskUpdate = (updated: CalendarTask, previousDate: string) => {
    setTasks((current) => {
      const filtered = current.filter((task) => task.id !== updated.id);
      if (updated.date !== selectedDateIso) {
        return filtered.sort(compareTasks);
      }
      return [...filtered, updated].sort(compareTasks);
    });

    setMonthTasks((current) => {
      let next = removeTaskFromMap(current, previousDate, updated.id);
      if (isDateInCurrentMonth(updated.date)) {
        const existing = next[updated.date] ?? [];
        next = { ...next, [updated.date]: [...existing, updated].sort(compareTasks) };
      }
      return next;
    });
  };

  const buildPayload = (task: CalendarTask, overrides?: Partial<CalendarTask>) => ({
    title: overrides?.title ?? task.title,
    details: overrides?.details ?? task.details ?? "",
    date: overrides?.date ?? task.date,
    importance: overrides?.importance ?? task.importance ?? "Optional",
    difficulty: overrides?.difficulty ?? task.difficulty ?? "Medium",
    isCompleted: overrides?.isCompleted ?? task.isCompleted ?? false,
    startTime: overrides?.startTime ?? task.startTime ?? null,
    endTime: overrides?.endTime ?? task.endTime ?? null,
    attributes: overrides?.attributes ?? task.attributes ?? [],
    attributeShares: overrides?.attributeShares ?? task.attributeShares ?? [],
    skillIds: overrides?.skillIds ?? task.skillIds ?? [],
    habitId: overrides?.habitId ?? task.habitId ?? null
  });

  const openEditModal = (task: CalendarTask) => {
    setEditingTask(task);
    setEditDate(task.date);
    setEditStartTime(task.startTime ?? "");
    setEditEndTime(task.endTime ?? "");
    setFormError(null);
  };

  const closeEditModal = () => {
    setEditingTask(null);
    setEditDate("");
    setEditStartTime("");
    setEditEndTime("");
    setFormError(null);
    setSaving(false);
  };

  if (loading) {
    return <div className="card">Загрузка календаря...</div>;
  }

  if (!user) {
    return (
      <div className="card">
        <h2>Календарь</h2>
        <p className="muted">Войдите, чтобы распределять задачи по времени.</p>
      </div>
    );
  }

  return (
    <section className="card calendar-card">
      <div className="calendar-header">
        <div>
          <h2>Календарь</h2>
          <p className="muted">{monthTitle}</p>
        </div>
        <div className="calendar-legend">
          <span className="legend-dot" />
          Сегодня
        </div>
      </div>

      <div className="calendar-grid">
        {WEEKDAYS.map((day) => (
          <div key={day} className="calendar-weekday">
            {day}
          </div>
        ))}

        {cells.map((cell, index) => {
          if (!cell) {
            return <div key={`empty-${index}`} className="calendar-cell muted" />;
          }

          const isToday =
            cell.date.getDate() === today.getDate() &&
            cell.date.getMonth() === today.getMonth() &&
            cell.date.getFullYear() === today.getFullYear();

          const isSelected =
            cell.date.getDate() === selectedDate.getDate() &&
            cell.date.getMonth() === selectedDate.getMonth() &&
            cell.date.getFullYear() === selectedDate.getFullYear();

          const dayIso = formatLocalDate(cell.date);
          const dayTasks = monthTasks[dayIso] ?? [];

          return (
            <button
              key={cell.day}
              type="button"
              className={`calendar-cell ${isToday ? "is-today" : ""} ${isSelected ? "is-selected" : ""}`}
              onClick={() => setSelectedDate(cell.date)}
            >
              <div className="calendar-cell-head">
                <span className="calendar-day">{cell.day}</span>
              </div>
              {dayTasks.length > 0 && (
                <div className="calendar-cell-tasks">
                  {dayTasks.slice(0, 2).map((task) => (
                    <div key={task.id} className={`calendar-task-preview ${task.isCompleted ? "is-completed" : ""}`}>
                      {task.startTime ? `${task.startTime} ` : ""}
                      {task.title}
                    </div>
                  ))}
                  {dayTasks.length > 2 && <div className="calendar-task-more">+{dayTasks.length - 2}</div>}
                </div>
              )}
            </button>
          );
        })}
      </div>

      <div className="calendar-footer">
        <div className="calendar-actions">
          <div className="muted">
            Выбран день: {selectedLabel}. Создавайте задачи во вкладке{" "}
            <Link to="/tasks" className="inline-link">
              Список дел
            </Link>
            .
          </div>
        </div>
      </div>

      <div className="calendar-tasks">
        <h3>План на день</h3>
        {monthTasksError && <div className="error">{monthTasksError}</div>}
        {tasksError && <div className="error">{tasksError}</div>}

        {tasks.length === 0 ? (
          <div className="muted">На выбранный день задач нет.</div>
        ) : (
          <div className="list split-list">
            <div>
              <h4>Со временем</h4>
              {tasksWithTime.length === 0 ? (
                <div className="muted">Задач со временем нет.</div>
              ) : (
                tasksWithTime.map((task) => (
                  <TaskRow
                    key={task.id}
                    task={task}
                    skillNameById={skillNameById}
                    skillGradientById={skillGradientById}
                    onToggleComplete={(nextCompleted) => {
                      const payload = buildPayload(task, { isCompleted: nextCompleted });
                      api
                        .patch<CalendarTask>(`/tasks/${task.id}`, payload)
                        .then((response) => applyTaskUpdate(response, task.date))
                        .catch((err) => {
                          if (err instanceof ApiError) {
                            setTasksError(err.message);
                          } else {
                            setTasksError("Не удалось обновить статус.");
                          }
                        });
                    }}
                    onEdit={() => openEditModal(task)}
                  />
                ))
              )}
            </div>

            <div>
              <h4>Без времени</h4>
              {tasksWithoutTime.length === 0 ? (
                <div className="muted">Все задачи распределены по времени.</div>
              ) : (
                tasksWithoutTime.map((task) => (
                  <TaskRow
                    key={task.id}
                    task={task}
                    skillNameById={skillNameById}
                    skillGradientById={skillGradientById}
                    onToggleComplete={(nextCompleted) => {
                      const payload = buildPayload(task, { isCompleted: nextCompleted });
                      api
                        .patch<CalendarTask>(`/tasks/${task.id}`, payload)
                        .then((response) => applyTaskUpdate(response, task.date))
                        .catch((err) => {
                          if (err instanceof ApiError) {
                            setTasksError(err.message);
                          } else {
                            setTasksError("Не удалось обновить статус.");
                          }
                        });
                    }}
                    onEdit={() => openEditModal(task)}
                  />
                ))
              )}
            </div>
          </div>
        )}
      </div>

      {editingTask && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-header">
              <h3>Распределение по времени</h3>
              <button className="ghost" type="button" onClick={closeEditModal}>
                Закрыть
              </button>
            </div>

            <form
              className="form"
              onSubmit={(event) => {
                event.preventDefault();

                if ((editStartTime && !editEndTime) || (!editStartTime && editEndTime)) {
                  setFormError("Укажите и время начала, и время окончания.");
                  return;
                }

                if (editStartTime && editEndTime) {
                  const startMinutes = parseTime(editStartTime);
                  const endMinutes = parseTime(editEndTime);
                  if (startMinutes === null || endMinutes === null) {
                    setFormError("Введите время в формате ЧЧ:ММ.");
                    return;
                  }
                  if (startMinutes >= endMinutes) {
                    setFormError("Время окончания должно быть позже начала.");
                    return;
                  }
                }

                setFormError(null);
                setSaving(true);

                const payload = buildPayload(editingTask, {
                  date: editDate,
                  startTime: editStartTime || null,
                  endTime: editEndTime || null
                });

                api
                  .patch<CalendarTask>(`/tasks/${editingTask.id}`, payload)
                  .then((response) => {
                    applyTaskUpdate(response, editingTask.date);
                    closeEditModal();
                  })
                  .catch((err) => {
                    if (err instanceof ApiError) {
                      setFormError(err.message);
                    } else {
                      setFormError("Не удалось сохранить изменения.");
                    }
                  })
                  .finally(() => setSaving(false));
              }}
            >
              <label className="field">
                <span>Задача</span>
                <input type="text" value={editingTask.title} disabled />
              </label>

              <label className="field">
                <span>Дата</span>
                <input type="date" value={editDate} onChange={(event) => setEditDate(event.target.value)} required />
              </label>

              <div className="field">
                <span>Интервал времени</span>
                <div className="time-row">
                  <label className="time-field">
                    <span>С</span>
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="09:00"
                      value={editStartTime}
                      onChange={(event) => setEditStartTime(formatTimeInput(event.target.value))}
                    />
                  </label>
                  <label className="time-field">
                    <span>До</span>
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="10:30"
                      value={editEndTime}
                      onChange={(event) => setEditEndTime(formatTimeInput(event.target.value))}
                    />
                  </label>
                </div>
              </div>

              {formError && <div className="error">{formError}</div>}

              <div className="button-row">
                <button className="primary" type="submit" disabled={saving}>
                  {saving ? "Сохраняем..." : "Сохранить"}
                </button>
                <button className="ghost" type="button" onClick={closeEditModal}>
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

type TaskRowProps = {
  task: CalendarTask;
  skillNameById: Map<string, string>;
  skillGradientById: Map<string, string | null>;
  onToggleComplete: (value: boolean) => void;
  onEdit: () => void;
};

function TaskRow({
  task,
  skillNameById,
  skillGradientById,
  onToggleComplete,
  onEdit
}: TaskRowProps) {
  const skillTags = (task.skillIds ?? [])
    .map((id) => ({
      id,
      label: skillNameById.get(id),
      gradient: skillGradientById.get(id) ?? null
    }))
    .filter(
      (item): item is { id: string; label: string; gradient: string | null } => Boolean(item.label)
    );
  const attributeTags = task.attributes
    .map((attribute) => ({
      value: attribute,
      label: ATTRIBUTE_OPTIONS.find((option) => option.value === attribute)?.label
    }))
    .filter((item): item is { value: string; label: string } => Boolean(item.label));

  return (
    <div className={`list-item ${task.isCompleted ? "is-completed" : ""}`}>
      <div className="task-row">
        <label className="task-check">
          <input
            type="checkbox"
            checked={task.isCompleted}
            aria-label="Отметить выполнено"
            onChange={() => onToggleComplete(!task.isCompleted)}
          />
        </label>
        <div className="task-main">
          <div className="task-title-row">
            <div className="list-title">{task.title}</div>
            <div className="list-tags inline">
              <span className={`pill importance-pill ${task.importance.toLowerCase()}`}>
                {IMPORTANCE_LABELS[task.importance]}
              </span>
              {attributeTags.map((tag) => (
                <span key={tag.value} className="pill attribute-pill" data-attribute={tag.value}>
                  {tag.label}
                </span>
              ))}
              {skillTags.map((item) => (
                <span
                  key={item.id}
                  className="pill skill-pill"
                  style={item.gradient ? { backgroundImage: item.gradient } : undefined}
                >
                  {item.label}
                </span>
              ))}
            </div>
          </div>
          <div className="list-meta">
            {task.startTime && task.endTime ? `${task.startTime} — ${task.endTime}` : "Без времени"}
            {" · "}
            {DIFFICULTY_LABELS[task.difficulty]}
            {" · "}
            XP: {task.experienceAwarded}
            {task.isFirstTaskBonusApplied ? " · x2 за первую задачу дня" : ""}
          </div>
        </div>
      </div>
      <div className="list-actions">
        <button className="ghost" type="button" onClick={onEdit}>
          Время
        </button>
      </div>
    </div>
  );
}
