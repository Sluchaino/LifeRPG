export type AttributeType =
  | "Strength"
  | "Endurance"
  | "Health"
  | "Intelligence"
  | "Wisdom"
  | "Agility"
  | "Charisma"
  | "Discipline";

export const ATTRIBUTE_OPTIONS: { value: AttributeType; label: string }[] = [
  { value: "Strength", label: "Сила" },
  { value: "Endurance", label: "Выносливость" },
  { value: "Health", label: "Здоровье" },
  { value: "Intelligence", label: "Интеллект" },
  { value: "Wisdom", label: "Мудрость" },
  { value: "Agility", label: "Ловкость" },
  { value: "Charisma", label: "Харизма" },
  { value: "Discipline", label: "Дисциплина" }
];

export const SKILL_ATTRIBUTE_OPTIONS = ATTRIBUTE_OPTIONS.filter(
  (option) => option.value !== "Discipline"
);

export const ATTRIBUTE_LABELS = ATTRIBUTE_OPTIONS.reduce<Record<string, string>>(
  (acc, option) => {
    acc[option.value] = option.label;
    return acc;
  },
  {}
);

export const ATTRIBUTE_DESCRIPTIONS: Record<AttributeType, string> = {
  Strength:
    "Физическая мощь, мышечный тонус, способность выполнять тяжёлую работу.",
  Endurance:
    "Общая физическая выносливость, здоровье сердечно‑сосудистой системы, иммунитет, качество сна.",
  Health:
    "Запас жизненной энергии и скорость восстановления организма после нагрузок.",
  Intelligence:
    "Способность к обучению, память, логическое мышление, решение сложных задач.",
  Wisdom:
    "Осознанность, эмоциональный интеллект, умение планировать, рефлексия, медитация.",
  Agility:
    "Координация движений, гибкость, скорость реакции, мелкая моторика.",
  Charisma:
    "Навыки общения, умение убеждать, публичные выступления, внешний вид, юмор.",
  Discipline:
    "Способность заставлять себя делать то, что нужно, даже когда не хочется; сила привычки."
};

const ATTRIBUTE_SOFT_COLORS: Record<AttributeType, string> = {
  Strength: "rgba(255, 107, 107, 0.18)",
  Endurance: "rgba(255, 179, 71, 0.18)",
  Health: "rgba(110, 231, 183, 0.18)",
  Intelligence: "rgba(125, 211, 252, 0.18)",
  Wisdom: "rgba(94, 234, 212, 0.18)",
  Agility: "rgba(163, 230, 53, 0.18)",
  Charisma: "rgba(244, 114, 182, 0.18)",
  Discipline: "rgba(250, 204, 21, 0.18)"
};

export function buildAttributeGradient(attributes: string[]): string | null {
  const colors = attributes
    .map((attr) => ATTRIBUTE_SOFT_COLORS[attr as AttributeType])
    .filter(Boolean);

  if (colors.length === 0) {
    return null;
  }

  if (colors.length === 1) {
    return `linear-gradient(135deg, ${colors[0]} 0%, ${colors[0]} 100%)`;
  }

  const step = 100 / (colors.length - 1);
  const stops = colors
    .map((color, index) => `${color} ${Math.round(index * step)}%`)
    .join(", ");

  return `linear-gradient(135deg, ${stops})`;
}
