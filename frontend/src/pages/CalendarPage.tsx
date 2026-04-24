import { useEffect, useRef, useState } from "react";
import { ATTRIBUTE_OPTIONS, AttributeType, buildAttributeGradient } from "../shared/attributes";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

type CalendarTask = {
  id: string;
  date: string;
  title: string;
  details?: string | null;
  difficulty: Difficulty;
  isCompleted: boolean;
  startTime?: string | null;
  endTime?: string | null;
  attributes: string[];
  skillIds?: string[];
};

type CalendarTaskMap = Record<string, CalendarTask[]>;

type UserSkill = {
  userSkillId: string;
  name: string;
  level: number;
  currentUses: number;
  requiredUsesForNextLevel: number;
  streakDays: number;
  attributes: string[];
};

type Difficulty = "Easy" | "Medium" | "Hard";

const DIFFICULTY_OPTIONS: { value: Difficulty; label: string }[] = [
  { value: "Easy", label: "Лёгкая" },
  { value: "Medium", label: "Средняя" },
  { value: "Hard", label: "Сложная" }
];

const DIFFICULTY_LABELS: Record<Difficulty, string> = {
  Easy: "Лёгкая",
  Medium: "Средняя",
  Hard: "Сложная"
};

const formatLocalDate = (date: Date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
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

const compareTasks = (left: CalendarTask, right: CalendarTask) => {
  const parse = (value?: string | null) => {
    if (!value) {
      return Number.MAX_SAFE_INTEGER;
    }
    const match = value.match(/^([01]\d|2[0-3]):([0-5]\d)$/);
    if (!match) {
      return Number.MAX_SAFE_INTEGER;
    }
    return Number(match[1]) * 60 + Number(match[2]);
  };

  const leftTime = parse(left.startTime);
  const rightTime = parse(right.startTime);
  if (leftTime !== rightTime) {
    return leftTime - rightTime;
  }
  return left.title.localeCompare(right.title, "ru");
};

export default function CalendarPage() {
  const { user, loading } = useAuth();
  const today = new Date();
  const [selectedDate, setSelectedDate] = useState<Date | null>(today);
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [editingTask, setEditingTask] = useState<CalendarTask | null>(null);
  const [title, setTitle] = useState("");
  const [details, setDetails] = useState("");
  const [taskDate, setTaskDate] = useState("");
  const [taskDateInput, setTaskDateInput] = useState("");
  const [dateError, setDateError] = useState<string | null>(null);
  const [difficulty, setDifficulty] = useState<Difficulty>("Medium");
  const [startTime, setStartTime] = useState("");
  const [endTime, setEndTime] = useState("");
  const [timeError, setTimeError] = useState<string | null>(null);
  const [selectedAttributes, setSelectedAttributes] = useState<AttributeType[]>([]);
  const [selectedSkills, setSelectedSkills] = useState<string[]>([]);
  const [skills, setSkills] = useState<UserSkill[]>([]);
  const [skillsError, setSkillsError] = useState<string | null>(null);
  const [tasks, setTasks] = useState<CalendarTask[]>([]);
  const [tasksError, setTasksError] = useState<string | null>(null);
  const [monthTasks, setMonthTasks] = useState<CalendarTaskMap>({});
  const [monthTasksError, setMonthTasksError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const datePickerRef = useRef<HTMLInputElement | null>(null);
  const { year, month, cells } = getMonthMatrix(today);
  const monthTitle = new Intl.DateTimeFormat("ru-RU", {
    month: "long",
    year: "numeric"
  }).format(new Date(year, month, 1));

  const selectedLabel = selectedDate
    ? new Intl.DateTimeFormat("ru-RU", {
        day: "numeric",
        month: "long",
        year: "numeric"
      }).format(selectedDate)
    : null;

  const selectedDateIso = selectedDate ? formatLocalDate(selectedDate) : null;
  const isEditing = Boolean(editingTask);
  const skillNameById = new Map(skills.map((skill) => [skill.userSkillId, skill.name]));
  const skillGradientById = new Map(
    skills.map((skill) => [skill.userSkillId, buildAttributeGradient(skill.attributes)])
  );

  useEffect(() => {
    if (isAddOpen) {
      document.body.classList.add("modal-open");
    } else {
      document.body.classList.remove("modal-open");
    }
    return () => {
      document.body.classList.remove("modal-open");
    };
  }, [isAddOpen]);

  useEffect(() => {
    if (!user || !selectedDateIso) {
      setTasks([]);
      return;
    }

    const loadTasks = async () => {
      setTasksError(null);
      try {
        const response = await api.get<CalendarTask[]>(
          `/calendar/tasks?date=${selectedDateIso}`
        );
        setTasks(response.sort(compareTasks));
      } catch (err) {
        if (err instanceof ApiError && err.status === 404) {
          setTasksError("Эндпоинт календаря не найден. Перезапустите сервер.");
          return;
        }
        setTasksError("Не удалось загрузить дела.");
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
        const response = await api.get<CalendarTask[]>(
          `/calendar/tasks/range?from=${from}&to=${to}`
        );
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
        if (err instanceof ApiError && err.status === 404) {
          setMonthTasksError("Эндпоинт календаря не найден. Перезапустите сервер.");
          return;
        }
        setMonthTasksError("Не удалось загрузить календарь.");
      }
    };

    void loadMonthTasks();
  }, [user, year, month]);

  useEffect(() => {
    if (!user) {
      setSkills([]);
      return;
    }

    const loadSkills = async () => {
      setSkillsError(null);
      try {
        const response = await api.get<UserSkill[]>("/skills");
        setSkills(response);
      } catch (err) {
        if (err instanceof ApiError) {
          setSkillsError(err.message);
        } else {
          setSkillsError("Не удалось загрузить навыки.");
        }
      }
    };

    void loadSkills();
  }, [user]);

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

  const formatDisplayDate = (isoDate: string) => {
    if (!isoDate) {
      return "";
    }
    const [yearPart, monthPart, dayPart] = isoDate.split("-");
    if (!yearPart || !monthPart || !dayPart) {
      return "";
    }
    return `${dayPart}.${monthPart}.${yearPart}`;
  };

  const parseDisplayDate = (value: string) => {
    const normalized = value.trim();
    if (!normalized) {
      return "";
    }

    const isoMatch = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (isoMatch) {
      return normalized;
    }

    const parts = normalized.split(".");
    if (parts.length !== 3) {
      return "";
    }
    const [day, monthPart, yearPart] = parts;
    if (yearPart.length !== 4 || monthPart.length !== 2 || day.length !== 2) {
      return "";
    }
    const iso = `${yearPart}-${monthPart}-${day}`;
    const date = new Date(`${iso}T00:00:00`);
    if (Number.isNaN(date.getTime())) {
      return "";
    }
    const isValid =
      date.getFullYear() === Number(yearPart) &&
      date.getMonth() + 1 === Number(monthPart) &&
      date.getDate() === Number(day);
    return isValid ? iso : "";
  };

  const normalizeAttributes = (attributes: string[]) =>
    attributes.filter((value): value is AttributeType =>
      ATTRIBUTE_OPTIONS.some((option) => option.value === value)
    );

  const openNewTask = () => {
    setEditingTask(null);
    setTitle("");
    setDetails("");
    const defaultDate = selectedDateIso ?? formatLocalDate(today);
    setTaskDate(defaultDate);
    setTaskDateInput(formatDisplayDate(defaultDate));
    setDateError(null);
    setDifficulty("Medium");
    setStartTime("");
    setEndTime("");
    setSelectedAttributes([]);
    setSelectedSkills([]);
    setTimeError(null);
    setIsAddOpen(true);
  };

  const openEditTask = (task: CalendarTask) => {
    setEditingTask(task);
    setTitle(task.title);
    setDetails(task.details ?? "");
    setTaskDate(task.date);
    setTaskDateInput(formatDisplayDate(task.date));
    setDateError(null);
    setDifficulty(task.difficulty ?? "Medium");
    setStartTime(task.startTime ?? "");
    setEndTime(task.endTime ?? "");
    setSelectedAttributes(normalizeAttributes(task.attributes));
    setSelectedSkills(task.skillIds ?? []);
    setTimeError(null);
    setIsAddOpen(true);
  };

  const closeModal = () => {
    setIsAddOpen(false);
    setEditingTask(null);
    setTimeError(null);
    setDateError(null);
  };

  const toggleAttribute = (value: AttributeType) => {
    setSelectedAttributes((current) =>
      current.includes(value)
        ? current.filter((item) => item !== value)
        : [...current, value]
    );
  };

  const toggleSkill = (value: string) => {
    setSelectedSkills((current) =>
      current.includes(value)
        ? current.filter((item) => item !== value)
        : [...current, value]
    );
  };

  const isDateInCurrentMonth = (dateIso: string) => {
    const [yearPart, monthPart] = dateIso.split("-");
    return Number(yearPart) === year && Number(monthPart) === month + 1;
  };

  const removeTaskFromMap = (
    current: CalendarTaskMap,
    dateIso: string,
    taskId: string
  ) => {
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

  const applyTaskUpdate = (updated: CalendarTask, previousDate: string) => {
    setTasks((current) => {
      const filtered = current.filter((item) => item.id !== updated.id);
      if (selectedDateIso && updated.date !== selectedDateIso) {
        return filtered.sort(compareTasks);
      }
      return [...filtered, updated].sort(compareTasks);
    });
    setMonthTasks((current) => {
      let next = removeTaskFromMap(current, previousDate, updated.id);
      if (isDateInCurrentMonth(updated.date)) {
        const existing = next[updated.date] ?? [];
        next = {
          ...next,
          [updated.date]: [...existing, updated].sort(compareTasks)
        };
      }
      return next;
    });
  };

  const buildPayload = (task: CalendarTask, overrides?: Partial<CalendarTask>) => ({
    title: overrides?.title ?? task.title,
    details: overrides?.details ?? task.details ?? "",
    date: overrides?.date ?? task.date,
    difficulty: overrides?.difficulty ?? task.difficulty ?? "Medium",
    isCompleted: overrides?.isCompleted ?? task.isCompleted ?? false,
    startTime: overrides?.startTime ?? task.startTime ?? null,
    endTime: overrides?.endTime ?? task.endTime ?? null,
    attributes: overrides?.attributes ?? task.attributes ?? [],
    skillIds: overrides?.skillIds ?? task.skillIds ?? []
  });

  if (loading) {
    return <div className="card">Загрузка календаря...</div>;
  }

  if (!user) {
    return (
      <div className="card">
        <h2>Календарь</h2>
        <p className="muted">Войдите, чтобы планировать дела.</p>
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
            selectedDate &&
            cell.date.getDate() === selectedDate.getDate() &&
            cell.date.getMonth() === selectedDate.getMonth() &&
            cell.date.getFullYear() === selectedDate.getFullYear();

          const dayIso = formatLocalDate(cell.date);
          const dayTasks = monthTasks[dayIso] ?? [];

          return (
            <button
              key={cell.day}
              type="button"
              className={`calendar-cell ${isToday ? "is-today" : ""} ${
                isSelected ? "is-selected" : ""
              }`}
              onClick={() => setSelectedDate(cell.date)}
            >
              <div className="calendar-cell-head">
                <span className="calendar-day">{cell.day}</span>
              </div>
              {dayTasks.length > 0 && (
                <div className="calendar-cell-tasks">
                  {dayTasks.slice(0, 2).map((task) => (
                    <div
                      key={task.id}
                      className={`calendar-task-preview ${
                        task.isCompleted ? "is-completed" : ""
                      }`}
                    >
                      {task.startTime ? `${task.startTime} ` : ""}
                      {task.title}
                    </div>
                  ))}
                  {dayTasks.length > 2 && (
                    <div className="calendar-task-more">+{dayTasks.length - 2}</div>
                  )}
                </div>
              )}
            </button>
          );
        })}
      </div>

      <div className="calendar-footer">
        {selectedLabel ? (
          <div className="calendar-actions">
            <div className="muted">Выбран день: {selectedLabel}</div>
            <button className="primary" type="button" onClick={openNewTask}>
              Добавить дело
            </button>
          </div>
        ) : (
          <p className="muted">Выберите день, чтобы добавить дело.</p>
        )}
      </div>

      <div className="calendar-tasks">
        <h3>План на день</h3>
        {monthTasksError && <div className="error">{monthTasksError}</div>}
        {tasksError && <div className="error">{tasksError}</div>}
        {tasks.length === 0 ? (
          <div className="muted">Дел пока нет.</div>
        ) : (
          <div className="list">
            {tasks.map((task) => {
              const skillTags = (task.skillIds ?? [])
                .map((id) => ({
                  id,
                  label: skillNameById.get(id),
                  gradient: skillGradientById.get(id) ?? null
                }))
                .filter(
                  (item): item is { id: string; label: string; gradient: string | null } =>
                    Boolean(item.label)
                );
              const attributeTags = task.attributes
                .map((attribute) => ({
                  value: attribute,
                  label: ATTRIBUTE_OPTIONS.find((option) => option.value === attribute)?.label
                }))
                .filter(
                  (item): item is { value: string; label: string } => Boolean(item.label)
                );

              return (
                <div
                  key={task.id}
                  className={`list-item ${task.isCompleted ? "is-completed" : ""}`}
                >
                  <div className="task-row">
                    <label className="task-check">
                      <input
                        type="checkbox"
                        checked={task.isCompleted}
                        aria-label="Отметить выполнено"
                        title="Отметить выполнено"
                        onChange={() => {
                          const payload = buildPayload(task, {
                            isCompleted: !task.isCompleted
                          });
                          api
                            .patch<CalendarTask>(`/calendar/tasks/${task.id}`, payload)
                            .then((response) => {
                              applyTaskUpdate(response, task.date);
                            })
                            .catch(() => {
                              setTasksError("Не удалось обновить статус.");
                            });
                        }}
                      />
                    </label>
                    <div className="task-main">
                    <div className="task-title-row">
                      <div className="list-title">{task.title}</div>
                      {(attributeTags.length > 0 || skillTags.length > 0) && (
                        <div className="list-tags inline">
                          {attributeTags.map((tag) => (
                            <span
                              key={tag.value}
                              className="pill attribute-pill"
                              data-attribute={tag.value}
                            >
                              {tag.label}
                            </span>
                          ))}
                          {skillTags.map((item) => (
                            <span
                              key={item.id}
                              className="pill skill-pill"
                              style={
                                item.gradient
                                  ? { backgroundImage: item.gradient }
                                  : undefined
                              }
                            >
                              {item.label}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                    <div className="list-meta">
                      {task.startTime && task.endTime
                        ? `${task.startTime} — ${task.endTime}`
                        : "Без времени"}
                      {" · "}
                      {DIFFICULTY_LABELS[task.difficulty ?? "Medium"]}
                      {task.details ? ` · ${task.details}` : ""}
                    </div>
                    </div>
                  </div>
                  <div className="list-actions">
                    <button className="ghost" type="button" onClick={() => openEditTask(task)}>
                      Редактировать
                    </button>
                    <button
                      className="ghost danger"
                      type="button"
                      onClick={() => {
                        if (!window.confirm("Удалить дело?")) {
                          return;
                        }
                        api
                          .delete(`/calendar/tasks/${task.id}`)
                          .then(() => {
                            setTasks((current) =>
                              current.filter((item) => item.id !== task.id)
                            );
                            setMonthTasks((current) =>
                              removeTaskFromMap(current, task.date, task.id)
                            );
                          })
                          .catch(() => {
                            setTasksError("Не удалось удалить дело.");
                          });
                      }}
                    >
                      Удалить
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {isAddOpen && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-header">
              <h3>{isEditing ? "Редактирование дела" : "Новое дело"}</h3>
              <button className="ghost" type="button" onClick={closeModal}>
                Закрыть
              </button>
            </div>
            <form
              className="form"
              onSubmit={(event) => {
                event.preventDefault();
                if ((startTime && !endTime) || (!startTime && endTime)) {
                  setTimeError("Укажите оба значения времени.");
                  return;
                }
                if (startTime && endTime) {
                  const startMinutes = parseTime(startTime);
                  const endMinutes = parseTime(endTime);
                  if (startMinutes === null || endMinutes === null) {
                    setTimeError("Введите время в формате ЧЧ:ММ.");
                    return;
                  }
                  if (startMinutes >= endMinutes) {
                    setTimeError("Время окончания должно быть позже начала.");
                    return;
                  }
                }

                if (!taskDate) {
                  setDateError("Дата не выбрана.");
                  return;
                }

                setTimeError(null);
                setDateError(null);
                setSaving(true);

                const payload = {
                  title,
                  details,
                  date: taskDate,
                  difficulty,
                  isCompleted: editingTask?.isCompleted ?? false,
                  startTime: startTime || null,
                  endTime: endTime || null,
                  attributes: selectedAttributes,
                  skillIds: selectedSkills
                };

                const request = editingTask
                  ? api.patch<CalendarTask>(`/calendar/tasks/${editingTask.id}`, payload)
                  : api.post<CalendarTask>("/calendar/tasks", payload);

                request
                  .then((response) => {
                    const previousDate = editingTask?.date ?? response.date;
                    applyTaskUpdate(response, previousDate);
                    setTitle("");
                    setDetails("");
                    setStartTime("");
                    setEndTime("");
                    setSelectedAttributes([]);
                    setSelectedSkills([]);
                    setEditingTask(null);
                    setIsAddOpen(false);
                  })
                  .catch((err) => {
                    if (err instanceof ApiError) {
                      setTimeError(err.message);
                    } else {
                      setTimeError(
                        isEditing ? "Не удалось обновить дело." : "Не удалось сохранить дело."
                      );
                    }
                  })
                  .finally(() => {
                    setSaving(false);
                  });
              }}
            >
              <label className="field">
                <span>Название</span>
                <input
                  type="text"
                  placeholder="Например: Отчёт по проекту"
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                  required
                />
              </label>
              <label className="field">
                <span>Дата</span>
                <div className="date-input">
                  <input
                    className="date-manual"
                    type="text"
                    inputMode="numeric"
                    placeholder="ДД.ММ.ГГГГ"
                    value={taskDateInput}
                    onChange={(event) => {
                      const nextValue = event.target.value;
                      setTaskDateInput(nextValue);
                      const parsed = parseDisplayDate(nextValue);
                      if (parsed) {
                        setTaskDate(parsed);
                        setDateError(null);
                      }
                    }}
                    onBlur={() => {
                      if (!taskDateInput.trim()) {
                        setDateError("Дата не выбрана.");
                        return;
                      }
                      const parsed = parseDisplayDate(taskDateInput);
                      if (!parsed) {
                        setDateError("Введите дату в формате ДД.ММ.ГГГГ.");
                      } else {
                        setDateError(null);
                        setTaskDate(parsed);
                        setTaskDateInput(formatDisplayDate(parsed));
                      }
                    }}
                    required
                  />
                  <button
                    className="date-icon"
                    type="button"
                    aria-label="Открыть календарь"
                    onClick={() => {
                      const picker = datePickerRef.current;
                      if (!picker) {
                        return;
                      }
                      if ("showPicker" in picker) {
                        (picker as HTMLInputElement & { showPicker?: () => void }).showPicker?.();
                      } else {
                        picker.click();
                      }
                      picker.focus();
                    }}
                  >
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <rect x="3.5" y="5.5" width="17" height="15" rx="2" />
                      <path d="M7 3.5v4" />
                      <path d="M17 3.5v4" />
                      <path d="M3.5 9.5h17" />
                    </svg>
                  </button>
                  <input
                    ref={datePickerRef}
                    className="date-hidden"
                    type="date"
                    value={taskDate}
                    onChange={(event) => {
                      const nextValue = event.target.value;
                      setTaskDate(nextValue);
                      setTaskDateInput(formatDisplayDate(nextValue));
                      setDateError(null);
                    }}
                    tabIndex={-1}
                    aria-hidden="true"
                  />
                </div>
                {dateError && <div className="error">{dateError}</div>}
              </label>
              <label className="field">
                <span>Описание</span>
                <input
                  type="text"
                  placeholder="Короткие детали"
                  value={details}
                  onChange={(event) => setDetails(event.target.value)}
                />
              </label>
              <div className="field">
                <span>Сложность</span>
                <div className="difficulty-picker">
                  {DIFFICULTY_OPTIONS.map((option) => (
                    <label
                      key={option.value}
                      className={`chip difficulty-chip ${
                        difficulty === option.value ? "is-active" : ""
                      }`}
                      data-difficulty={option.value}
                    >
                      <input
                        type="radio"
                        name="difficulty"
                        checked={difficulty === option.value}
                        onChange={() => setDifficulty(option.value)}
                      />
                      <span>{option.label}</span>
                    </label>
                  ))}
                </div>
              </div>
              <div className="field">
                <span>Время</span>
                <div className="time-row">
                  <label className="time-field">
                    <span>С</span>
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="09:00"
                      value={startTime}
                      onChange={(event) => {
                        setStartTime(formatTimeInput(event.target.value));
                        setTimeError(null);
                      }}
                    />
                  </label>
                  <label className="time-field">
                    <span>До</span>
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="10:30"
                      value={endTime}
                      onChange={(event) => {
                        setEndTime(formatTimeInput(event.target.value));
                        setTimeError(null);
                      }}
                    />
                  </label>
                </div>
                {timeError && <div className="error">{timeError}</div>}
              </div>
              <div className="field">
                <span>Характеристики</span>
                <div className="attribute-picker compact">
                  {ATTRIBUTE_OPTIONS.map((option) => (
                    <label
                      key={option.value}
                      className="attribute-option"
                      data-attribute={option.value}
                    >
                      <div className="attribute-option-left">
                        <input
                          type="checkbox"
                          checked={selectedAttributes.includes(option.value)}
                          onChange={() => toggleAttribute(option.value)}
                        />
                        <span>{option.label}</span>
                      </div>
                    </label>
                  ))}
                </div>
              </div>
              <div className="field">
                <span>Навыки</span>
                {skillsError && <div className="error">{skillsError}</div>}
                {skills.length === 0 ? (
                  <div className="muted">
                    Пока нет навыков. Добавьте их в профиле.
                  </div>
                ) : (
                  <div className="chip-list">
                    {skills.map((skill) => (
                      <label
                        key={skill.userSkillId}
                        className="chip skill-pill"
                        style={
                          skillGradientById.get(skill.userSkillId)
                            ? { backgroundImage: skillGradientById.get(skill.userSkillId) ?? undefined }
                            : undefined
                        }
                      >
                        <input
                          type="checkbox"
                          checked={selectedSkills.includes(skill.userSkillId)}
                          onChange={() => toggleSkill(skill.userSkillId)}
                        />
                        <span>{skill.name}</span>
                      </label>
                    ))}
                  </div>
                )}
              </div>
              <div className="button-row">
                <button className="primary" type="submit" disabled={saving}>
                  {saving ? "Сохраняем..." : isEditing ? "Сохранить" : "Создать"}
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
