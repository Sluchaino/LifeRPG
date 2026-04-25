import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";
import {
  ATTRIBUTE_DESCRIPTIONS,
  ATTRIBUTE_LABELS,
  SKILL_ATTRIBUTE_OPTIONS,
  AttributeType,
  buildAttributeGradient
} from "../shared/attributes";

type ProfileResponse = {
  userId: string;
  login: string;
  createdAtUtc: string;
  level: number;
  totalExperience: number;
  currentLevelExperience: number;
  experienceToNextLevel: number;
  attributes: { type: string; value: number }[];
};

type SkillResponse = {
  userSkillId: string;
  name: string;
  level: number;
  currentUses: number;
  requiredUsesForNextLevel: number;
  streakDays: number;
  attributes: string[];
};

export default function ProfilePage() {
  const { user, loading } = useAuth();
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [skills, setSkills] = useState<SkillResponse[]>([]);
  const [name, setName] = useState("");
  const [selected, setSelected] = useState<AttributeType[]>([]);
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const canSubmit = useMemo(() => name.trim().length >= 2 && !busy, [name, busy]);
  const experiencePercent = profile
    ? Math.min(
        100,
        Math.round(
          (profile.currentLevelExperience / Math.max(1, profile.experienceToNextLevel)) *
            100
        )
      )
    : 0;

  useEffect(() => {
    if (!user) {
      setProfile(null);
      setSkills([]);
      return;
    }

    const load = async () => {
      try {
        const [profileResponse, skillsResponse] = await Promise.all([
          api.get<ProfileResponse>("/profile"),
          api.get<SkillResponse[]>("/skills")
        ]);
        setProfile(profileResponse);
        setSkills(skillsResponse);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("Не удалось загрузить профиль.");
        }
      }
    };

    void load();
  }, [user]);

  const toggleAttribute = (value: AttributeType) => {
    setSelected((current) =>
      current.includes(value)
        ? current.filter((item) => item !== value)
        : [...current, value]
    );
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) {
      return;
    }

    setError(null);
    setBusy(true);

    try {
      const response = await api.post<SkillResponse>("/skills", {
        name,
        attributes: selected
      });
      setSkills((current) =>
        [...current, response].sort((a, b) => a.name.localeCompare(b.name))
      );
      setName("");
      setSelected([]);
      setIsAddOpen(false);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось создать навык.");
      }
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return <div className="card">Загрузка профиля...</div>;
  }

  if (!user) {
    return (
      <div className="card">
        <h2>Профиль</h2>
        <p className="muted">Войдите, чтобы увидеть профиль персонажа.</p>
      </div>
    );
  }

  return (
    <section className="card profile-card">
      <h2>Профиль</h2>
      {error && <div className="error">{error}</div>}
      {profile && (
        <div className="experience-card">
          <div className="experience-head">
            <span className="experience-level">Уровень {profile.level}</span>
            <span className="experience-total">Всего опыта: {profile.totalExperience}</span>
          </div>
          <div className="experience-bar">
            <div
              className="experience-fill"
              style={{ width: `${experiencePercent}%` }}
            />
          </div>
          <div className="experience-meta">
            {profile.currentLevelExperience}/{profile.experienceToNextLevel} до следующего
            уровня
          </div>
        </div>
      )}

      <div className="profile-layout">
        <div>
          <h3>Характеристики</h3>
          {profile ? (
            <div className="attribute-list">
              {profile.attributes.map((attribute) => (
                <div
                  key={attribute.type}
                  className="attribute-row"
                  data-attribute={attribute.type}
                  title={
                    ATTRIBUTE_DESCRIPTIONS[attribute.type as AttributeType] ??
                    attribute.type
                  }
                >
                  <span className="attribute-name">
                    {ATTRIBUTE_LABELS[attribute.type] ?? attribute.type}
                  </span>
                  <span className="attribute-value">{attribute.value}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="muted">Данных профиля пока нет.</div>
          )}
        </div>

        <div>
          <div className="skills-header">
            <div>
              <h3>Навыки</h3>
              <p className="muted">Ваши навыки.</p>
            </div>
            <div className="skills-actions">
              <Link className="ghost" to="/skills">
                Подробнее
              </Link>
              <button className="primary" type="button" onClick={() => setIsAddOpen(true)}>
                Добавить навык
              </button>
            </div>
          </div>
          {skills.length === 0 ? (
            <div className="muted">Навыков нет. Добавьте первый.</div>
          ) : (
            <div className="list">
            {skills.map((skill) => (
              <div
                key={skill.userSkillId}
                className="list-item skill-card"
                style={{
                  backgroundImage: buildAttributeGradient(skill.attributes) ?? undefined
                }}
              >
                <div>
                  <div className="list-title">{skill.name}</div>
                    <div className="list-meta">
                      Уровень {skill.level} · Использования {skill.currentUses}/
                      {skill.requiredUsesForNextLevel} · Серия {skill.streakDays}
                    </div>
                    <div className="list-tags">
                      {skill.attributes.length === 0 ? (
                        <span className="pill muted">Без характеристик</span>
                      ) : (
                        skill.attributes.map((attr) => (
                          <span
                            key={attr}
                            className="pill attribute-pill"
                            data-attribute={attr}
                          >
                            {ATTRIBUTE_LABELS[attr] ?? attr}
                          </span>
                        ))
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {isAddOpen && (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-header">
              <h3>Добавить навык</h3>
              <button className="ghost" type="button" onClick={() => setIsAddOpen(false)}>
                Закрыть
              </button>
            </div>
            <form className="form" onSubmit={handleSubmit}>
              <label className="field">
                <span>Название навыка</span>
                <input
                  type="text"
                  placeholder="Например: Гитара"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                />
              </label>
              <div className="field">
                <span>Связанные характеристики</span>
                <div className="attribute-picker">
                  {SKILL_ATTRIBUTE_OPTIONS.map((option) => (
                    <label
                      key={option.value}
                      className="attribute-option"
                      data-attribute={option.value}
                    >
                      <div className="attribute-option-left">
                        <input
                          type="checkbox"
                          checked={selected.includes(option.value)}
                          onChange={() => toggleAttribute(option.value)}
                        />
                        <span>{option.label}</span>
                      </div>
                      <span className="attribute-option-desc">
                        {ATTRIBUTE_DESCRIPTIONS[option.value]}
                      </span>
                    </label>
                  ))}
                </div>
              </div>
              {error && <div className="error">{error}</div>}
              <div className="button-row">
                <button className="primary" type="submit" disabled={!canSubmit}>
                  {busy ? "Сохраняем..." : "Добавить навык"}
                </button>
                <button className="ghost" type="button" onClick={() => setIsAddOpen(false)}>
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
