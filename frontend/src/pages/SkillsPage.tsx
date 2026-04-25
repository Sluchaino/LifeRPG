import { FormEvent, useEffect, useMemo, useState } from "react";
import { api, ApiError } from "../shared/api";
import { useAuth } from "../shared/auth";
import {
  ATTRIBUTE_DESCRIPTIONS,
  ATTRIBUTE_LABELS,
  SKILL_ATTRIBUTE_OPTIONS,
  AttributeType,
  buildAttributeGradient
} from "../shared/attributes";

type SkillResponse = {
  userSkillId: string;
  name: string;
  level: number;
  currentUses: number;
  requiredUsesForNextLevel: number;
  attributes: string[];
};

export default function SkillsPage() {
  const { user, loading } = useAuth();
  const [skills, setSkills] = useState<SkillResponse[]>([]);
  const [name, setName] = useState("");
  const [selected, setSelected] = useState<AttributeType[]>([]);
  const [editingSkillId, setEditingSkillId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState("");
  const [editingAttributes, setEditingAttributes] = useState<AttributeType[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const canSubmit = useMemo(() => name.trim().length >= 2 && !busy, [name, busy]);
  const canEditSubmit = useMemo(
    () => editingName.trim().length >= 2 && !busy,
    [editingName, busy]
  );

  useEffect(() => {
    if (!user) {
      setSkills([]);
      return;
    }

    const load = async () => {
      try {
        const response = await api.get<SkillResponse[]>("/skills");
        setSkills(response);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("Не удалось загрузить навыки.");
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

  const toggleEditingAttribute = (value: AttributeType) => {
    setEditingAttributes((current) =>
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

  const startEditing = (skill: SkillResponse) => {
    setEditingSkillId(skill.userSkillId);
    setEditingName(skill.name);
    setEditingAttributes(skill.attributes as AttributeType[]);
  };

  const cancelEditing = () => {
    setEditingSkillId(null);
    setEditingName("");
    setEditingAttributes([]);
  };

  const handleEditSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canEditSubmit || !editingSkillId) {
      return;
    }

    setError(null);
    setBusy(true);

    try {
      const response = await api.patch<SkillResponse>(`/skills/${editingSkillId}`, {
        name: editingName,
        attributes: editingAttributes
      });
      setSkills((current) =>
        current
          .map((skill) => (skill.userSkillId === editingSkillId ? response : skill))
          .sort((a, b) => a.name.localeCompare(b.name))
      );
      cancelEditing();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось обновить навык.");
      }
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (skillId: string) => {
    setError(null);
    try {
      await api.delete<void>(`/skills/${skillId}`);
      setSkills((current) => current.filter((skill) => skill.userSkillId !== skillId));
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Не удалось удалить навык.");
      }
    }
  };

  if (loading) {
    return <div className="card">Загрузка навыков...</div>;
  }

  if (!user) {
    return (
      <div className="card">
        <h2>Навыки</h2>
        <p className="muted">Войдите, чтобы управлять навыками.</p>
      </div>
    );
  }

  return (
    <section className="skills-grid">
      <div className="card">
        <h2>Добавить навык</h2>
        <p className="muted">
          Создайте личный навык и свяжите его с характеристиками.
        </p>
        <form className="form" onSubmit={handleSubmit}>
          <label className="field">
            <span>Название навыка</span>
            <input
              type="text"
              placeholder="Например: Гитара или Программирование"
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
          <button className="primary" type="submit" disabled={!canSubmit}>
            {busy ? "Сохраняем..." : "Добавить навык"}
          </button>
        </form>
      </div>

      <div className="card">
        <h2>Ваши навыки</h2>
        <p className="muted">Каждый навык уникален и виден только вам.</p>
        {skills.length > 0 && (
          <div className="stats-row">
            <div className="stat-box">
              <span className="stat-label">Всего навыков</span>
              <span className="stat-number">{skills.length}</span>
            </div>
            <div className="stat-box">
              <span className="stat-label">Средний уровень</span>
              <span className="stat-number">
                {(
                  skills.reduce((sum, skill) => sum + skill.level, 0) / skills.length
                ).toFixed(1)}
              </span>
            </div>
            <div className="stat-box">
              <span className="stat-label">Всего использований</span>
              <span className="stat-number">
                {skills.reduce((sum, skill) => sum + skill.currentUses, 0)}
              </span>
            </div>
            <div className="stat-box">
              <span className="stat-label">С привязкой к характеристикам</span>
              <span className="stat-number">
                {skills.filter((skill) => skill.attributes.length > 0).length}
              </span>
            </div>
          </div>
        )}
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
                    {skill.requiredUsesForNextLevel}
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
                <div className="list-actions">
                  <button
                    className="ghost"
                    type="button"
                    onClick={() => startEditing(skill)}
                  >
                    Редактировать
                  </button>
                  <button
                    className="ghost danger"
                    type="button"
                    onClick={() => void handleDelete(skill.userSkillId)}
                  >
                    Удалить
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {editingSkillId && (
        <div className="card edit-card">
          <h2>Редактировать навык</h2>
          <form className="form" onSubmit={handleEditSubmit}>
            <label className="field">
              <span>Название навыка</span>
              <input
                type="text"
                value={editingName}
                onChange={(event) => setEditingName(event.target.value)}
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
                        checked={editingAttributes.includes(option.value)}
                        onChange={() => toggleEditingAttribute(option.value)}
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
              <button className="primary" type="submit" disabled={!canEditSubmit}>
                {busy ? "Обновляем..." : "Сохранить"}
              </button>
              <button className="ghost" type="button" onClick={cancelEditing}>
                Отмена
              </button>
            </div>
          </form>
        </div>
      )}
    </section>
  );
}
