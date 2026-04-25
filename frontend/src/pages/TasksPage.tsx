import { useEffect, useMemo, useState } from "react";
import {
  ATTRIBUTE_OPTIONS,
  AttributeType,
  buildAttributeGradient
} from "../shared/attributes";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";
import {
  CalendarTask,
  Difficulty,
  DIFFICULTY_LABELS,
  DIFFICULTY_OPTIONS,
  IMPORTANCE_LABELS,
  IMPORTANCE_OPTIONS,
  TaskImportance,
  compareTasks,
  formatLocalDate
} from "../shared/tasks";

type UserSkill = {
  userSkillId: string;
  name: string;
  level: number;
  currentUses: number;
  requiredUsesForNextLevel: number;
  attributes: string[];
};

type HabitOption = {
  id: string;
  name: string;
};

type HabitsResponse = {
  habits: HabitOption[];
};

const normalizeAttributes = (attributes: string[]) =>
  attributes.filter((value): value is AttributeType =>
    ATTRIBUTE_OPTIONS.some((option) => option.value === value)
  );

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

export default function TasksPage() {
  const { user, loading } = useAuth();
  const today = useMemo(() => new Date(), []);
  const [selectedDate, setSelectedDate] = useState<Date>(today);
  const [tasks, setTasks] = useState<CalendarTask[]>([]);
  const [tasksError, setTasksError] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState<CalendarTask | null>(null);
  const [title, setTitle] = useState("");
  const [details, setDetails] = useState("");
  const [taskDate, setTaskDate] = useState(formatLocalDate(today));
  const [importance, setImportance] = useState<TaskImportance>("Important");
  const [difficulty, setDifficulty] = useState<Difficulty>("Medium");
  const [startTime, setStartTime] = useState("");
  const [endTime, setEndTime] = useState("");
  const [selectedAttributes, setSelectedAttributes] = useState<AttributeType[]>([]);
  const [selectedSkills, setSelectedSkills] = useState<string[]>([]);
  const [selectedHabitId, setSelectedHabitId] = useState("");
  const [skills, setSkills] = useState<UserSkill[]>([]);
  const [skillsError, setSkillsError] = useState<string | null>(null);
  const [habits, setHabits] = useState<HabitOption[]>([]);
  const [habitsError, setHabitsError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const selectedDateIso = formatLocalDate(selectedDate);
  const selectedLabel = new Intl.DateTimeFormat("ru-RU", {
    day: "numeric",
    month: "long",
    year: "numeric"
  }).format(selectedDate);

  const skillNameById = new Map(skills.map((skill) => [skill.userSkillId, skill.name]));
  const skillGradientById = new Map(
    skills.map((skill) => [skill.userSkillId, buildAttributeGradient(skill.attributes)])
  );

  useEffect(() => {
    if (isModalOpen) {
      document.body.classList.add("modal-open");
    } else {
      document.body.classList.remove("modal-open");
    }
    return () => {
      document.body.classList.remove("modal-open");
    };
  }, [isModalOpen]);

  useEffect(() => {
    if (!user) {
      setTasks([]);
      return;
    }

    const loadTasks = async () => {
      setTasksError(null);
      try {
        const response = await api.get<CalendarTask[]>(`/tasks?date=${selectedDateIso}`);
        setTasks(response.sort(compareTasks));
      } catch (err) {
        if (err instanceof ApiError) {
          setTasksError(err.message);
          return;
        }
        setTasksError("Не удалось загрузить список дел.");
      }
    };

    void loadTasks();
  }, [user, selectedDateIso]);

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
          return;
        }
        setSkillsError("Не удалось загрузить навыки.");
      }
    };

    void loadSkills();
  }, [user]);

  useEffect(() => {
    if (!user) {
      setHabits([]);
      return;
    }

    const loadHabits = async () => {
      setHabitsError(null);
      try {
        const response = await api.get<HabitsResponse>("/habits");
        setHabits(
          [...response.habits].sort((left, right) => left.name.localeCompare(right.name, "ru"))
        );
      } catch (err) {
        if (err instanceof ApiError) {
          setHabitsError(err.message);
          return;
        }
        setHabitsError("Не удалось загрузить привычки.");
      }
    };

    void loadHabits();
  }, [user]);

  const completedCount = tasks.filter((task) => task.isCompleted).length;
  const withTimeCount = tasks.filter((task) => task.startTime && task.endTime).length;

  const toggleAttribute = (value: AttributeType) => {
    setSelectedAttributes((current) =>
      current.includes(value) ? current.filter((item) => item !== value) : [...current, value]
    );
  };

  const toggleSkill = (value: string) => {
    setSelectedSkills((current) =>
      current.includes(value) ? current.filter((item) => item !== value) : [...current, value]
    );
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
    skillIds: overrides?.skillIds ?? task.skillIds ?? [],
    habitId: overrides?.habitId ?? task.habitId ?? null
  });

  const openCreateModal = () => {
    setEditingTask(null);
    setTitle("");
    setDetails("");
    setTaskDate(selectedDateIso);
    setImportance("Important");
    setDifficulty("Medium");
    setStartTime("");
    setEndTime("");
    setSelectedAttributes([]);
    setSelectedSkills([]);
    setSelectedHabitId("");
    setFormError(null);
    setIsModalOpen(true);
  };

  const openEditModal = (task: CalendarTask) => {
    setEditingTask(task);
    setTitle(task.title);
    setDetails(task.details ?? "");
    setTaskDate(task.date);
    setImportance(task.importance ?? "Optional");
    setDifficulty(task.difficulty ?? "Medium");
    setStartTime(task.startTime ?? "");
    setEndTime(task.endTime ?? "");
    setSelectedAttributes(normalizeAttributes(task.attributes));
    setSelectedSkills(task.skillIds ?? []);
    setSelectedHabitId(task.habitId ?? "");
    setFormError(null);
    setIsModalOpen(true);
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setEditingTask(null);
    setFormError(null);
  };

  const saveTask = async () => {
    if (title.trim().length < 2) {
      setFormError("Введите название от 2 символов.");
      return;
    }

    if ((startTime && !endTime) || (!startTime && endTime)) {
      setFormError("Укажите и время начала, и время окончания.");
      return;
    }

    if (startTime && endTime) {
      const startMinutes = parseTime(startTime);
      const endMinutes = parseTime(endTime);
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

    const payload = {
      title,
      details,
      date: taskDate,
      importance,
      difficulty,
      isCompleted: editingTask?.isCompleted ?? false,
      startTime: startTime || null,
      endTime: endTime || null,
      attributes: selectedAttributes,
      skillIds: selectedSkills,
      habitId: selectedHabitId || null
    };

    try {
      const response = editingTask
        ? await api.patch<CalendarTask>(`/tasks/${editingTask.id}`, payload)
        : await api.post<CalendarTask>("/tasks", payload);

      setTasks((current) => {
        const filtered = current.filter((task) => task.id !== response.id);
        if (response.date !== selectedDateIso) {
          return filtered.sort(compareTasks);
        }
        return [...filtered, response].sort(compareTasks);
      });

      setIsModalOpen(false);
      setEditingTask(null);
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.message);
      } else {
        setFormError("Не удалось сохранить задачу.");
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="card">Загрузка списка дел...</div>;
  }

  if (!user) {
    return (
      <div className="card">
        <h2>Список дел</h2>
        <p className="muted">Войдите, чтобы создавать и выполнять задачи.</p>
      </div>
    );
  }

  return (
    <section className="card">
      <div className="skills-header">
        <div>
          <h2>Список дел</h2>
          <p className="muted">Задачи на {selectedLabel}.</p>
        </div>
        <div className="skills-actions">
          <label className="field compact-field">
            <span>Дата</span>
            <input
              type="date"
              value={selectedDateIso}
              onChange={(event) => {
                if (!event.target.value) {
                  return;
                }
                setSelectedDate(new Date(`${event.target.value}T00:00:00`));
              }}
            />
          </label>
          <button className="primary" type="button" onClick={openCreateModal}>
            Добавить задачу
          </button>
        </div>
      </div>

      <div className="stats-row">
        <div className="stat-box">
          <span className="stat-label">Всего задач</span>
          <span className="stat-number">{tasks.length}</span>
        </div>
        <div className="stat-box">
          <span className="stat-label">Выполнено</span>
          <span className="stat-number">{completedCount}</span>
        </div>
        <div className="stat-box">
          <span className="stat-label">Со временем</span>
          <span className="stat-number">{withTimeCount}</span>
        </div>
        <div className="stat-box">
          <span className="stat-label">Без времени</span>
          <span className="stat-number">{Math.max(0, tasks.length - withTimeCount)}</span>
        </div>
      </div>

      {tasksError && <div className="error">{tasksError}</div>}

      {tasks.length === 0 ? (
        <div className="muted">На выбранную дату задач пока нет.</div>
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
              .filter((item): item is { value: string; label: string } => Boolean(item.label));

            return (
              <div key={task.id} className={`list-item ${task.isCompleted ? "is-completed" : ""}`}>
                <div className="task-row">
                  <label className="task-check">
                    <input
                      type="checkbox"
                      checked={task.isCompleted}
                      aria-label="Отметить выполнено"
                      onChange={() => {
                        const payload = buildPayload(task, { isCompleted: !task.isCompleted });
                        api
                          .patch<CalendarTask>(`/tasks/${task.id}`, payload)
                          .then((response) => {
                            setTasks((current) =>
                              current
                                .map((item) => (item.id === response.id ? response : item))
                                .sort(compareTasks)
                            );
                          })
                          .catch((err) => {
                            if (err instanceof ApiError) {
                              setTasksError(err.message);
                            } else {
                              setTasksError("Не удалось обновить статус.");
                            }
                          });
                      }}
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
                        {skillTags.map((skill) => (
                          <span
                            key={skill.id}
                            className="pill skill-pill"
                            style={skill.gradient ? { backgroundImage: skill.gradient } : undefined}
                          >
                            {skill.label}
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
                      {task.details ? ` · ${task.details}` : ""}
                    </div>
                  </div>
                </div>
                <div className="list-actions">
                  <button className="ghost" type="button" onClick={() => openEditModal(task)}>
                    Редактировать
                  </button>
                  <button
                    className="ghost danger"
                    type="button"
                    onClick={() => {
                      if (!window.confirm("Удалить задачу?")) {
                        return;
                      }

                      api
                        .delete(`/tasks/${task.id}`)
                        .then(() => {
                          setTasks((current) => current.filter((item) => item.id !== task.id));
                        })
                        .catch((err) => {
                          if (err instanceof ApiError) {
                            setTasksError(err.message);
                          } else {
                            setTasksError("Не удалось удалить задачу.");
                          }
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

      {isModalOpen && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-header">
              <h3>{editingTask ? "Редактирование задачи" : "Новая задача"}</h3>
              <button className="ghost" type="button" onClick={closeModal}>
                Закрыть
              </button>
            </div>

            <form
              className="form"
              onSubmit={(event) => {
                event.preventDefault();
                void saveTask();
              }}
            >
              <label className="field">
                <span>Название</span>
                <input
                  type="text"
                  value={title}
                  placeholder="Например: Подготовить отчёт"
                  onChange={(event) => setTitle(event.target.value)}
                  required
                />
              </label>

              <label className="field">
                <span>Дата</span>
                <input
                  type="date"
                  value={taskDate}
                  onChange={(event) => setTaskDate(event.target.value)}
                  required
                />
              </label>

              <label className="field">
                <span>Описание</span>
                <input
                  type="text"
                  value={details}
                  placeholder="Короткие детали"
                  onChange={(event) => setDetails(event.target.value)}
                />
              </label>

              <div className="field">
                <span>Важность</span>
                <div className="importance-grid">
                  {IMPORTANCE_OPTIONS.map((option) => (
                    <label
                      key={option.value}
                      className={`importance-option ${importance === option.value ? "is-active" : ""}`}
                      data-importance={option.value}
                    >
                      <input
                        type="radio"
                        name="importance"
                        checked={importance === option.value}
                        onChange={() => setImportance(option.value)}
                      />
                      <div className="importance-content">
                        <span className="importance-title">{option.label}</span>
                        <span className="importance-desc">{option.description}</span>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              <div className="field">
                <span>Сложность</span>
                <div className="difficulty-picker">
                  {DIFFICULTY_OPTIONS.map((option) => (
                    <label
                      key={option.value}
                      className={`chip difficulty-chip ${difficulty === option.value ? "is-active" : ""}`}
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
                        setFormError(null);
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
                        setFormError(null);
                      }}
                    />
                  </label>
                </div>
              </div>

              <div className="field">
                <span>Характеристики</span>
                <div className="attribute-picker compact">
                  {ATTRIBUTE_OPTIONS.map((option) => (
                    <label key={option.value} className="attribute-option" data-attribute={option.value}>
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
                  <div className="muted">Навыков пока нет. Добавьте их в профиле.</div>
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

              <div className="field">
                <span>Связанная привычка</span>
                {habitsError && <div className="error">{habitsError}</div>}
                {habits.length === 0 ? (
                  <div className="muted">Пока нет привычек для связи.</div>
                ) : (
                  <select value={selectedHabitId} onChange={(event) => setSelectedHabitId(event.target.value)}>
                    <option value="">Без привычки</option>
                    {habits.map((habit) => (
                      <option key={habit.id} value={habit.id}>
                        {habit.name}
                      </option>
                    ))}
                  </select>
                )}
              </div>

              {formError && <div className="error">{formError}</div>}

              <div className="button-row">
                <button className="primary" type="submit" disabled={saving}>
                  {saving ? "Сохраняем..." : editingTask ? "Сохранить" : "Создать"}
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
