export type Difficulty = "Easy" | "Medium" | "Hard";
export type TaskImportance = "Required" | "Important" | "Optional";

export type CalendarTask = {
  id: string;
  date: string;
  title: string;
  details?: string | null;
  importance: TaskImportance;
  difficulty: Difficulty;
  isCompleted: boolean;
  startTime?: string | null;
  endTime?: string | null;
  attributes: string[];
  skillIds?: string[];
  habitId?: string | null;
  habitName?: string | null;
  experienceAwarded: number;
  isFirstTaskBonusApplied: boolean;
};

export type CalendarTaskMap = Record<string, CalendarTask[]>;

export const DIFFICULTY_OPTIONS: Array<{ value: Difficulty; label: string }> = [
  { value: "Easy", label: "Лёгкая" },
  { value: "Medium", label: "Средняя" },
  { value: "Hard", label: "Сложная" }
];

export const DIFFICULTY_LABELS: Record<Difficulty, string> = {
  Easy: "Лёгкая",
  Medium: "Средняя",
  Hard: "Сложная"
};

export const IMPORTANCE_OPTIONS: Array<{
  value: TaskImportance;
  label: string;
  description: string;
}> = [
  {
    value: "Required",
    label: "Обязательно",
    description: "Одна задача в день. Максимальный базовый опыт."
  },
  {
    value: "Important",
    label: "Важно",
    description: "До трёх задач в день. Средний базовый опыт."
  },
  {
    value: "Optional",
    label: "Необязательно",
    description: "Без ограничений. Базовый опыт ниже."
  }
];

export const IMPORTANCE_LABELS: Record<TaskImportance, string> = {
  Required: "Обязательно",
  Important: "Важно",
  Optional: "Необязательно"
};

export const formatLocalDate = (date: Date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
};

export const compareTasks = (left: CalendarTask, right: CalendarTask) => {
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
