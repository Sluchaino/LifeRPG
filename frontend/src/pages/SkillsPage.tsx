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
import { SkillAttributeDistributionSlider } from "../shared/SkillAttributeDistributionSlider";
import {
  SkillAttributeShare,
  hasInvalidSkillAttributeShares,
  rebalanceSkillAttributeShares
} from "../shared/skillAttributeShares";

type SkillResponse = {
  userSkillId: string;
  name: string;
  level: number;
  currentUses: number;
  requiredUsesForNextLevel: number;
  attributes: string[];
  attributeShares: {
    attributeType: string;
    percent: number;
  }[];
};

export default function SkillsPage() {
  const { user, loading } = useAuth();
  const [skills, setSkills] = useState<SkillResponse[]>([]);
  const [name, setName] = useState("");
  const [selected, setSelected] = useState<AttributeType[]>([]);
  const [selectedShares, setSelectedShares] = useState<SkillAttributeShare[]>([]);
  const [editingSkillId, setEditingSkillId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState("");
  const [editingAttributes, setEditingAttributes] = useState<AttributeType[]>([]);
  const [editingShares, setEditingShares] = useState<SkillAttributeShare[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const hasInvalidCreateShareState = useMemo(
    () => hasInvalidSkillAttributeShares(selectedShares),
    [selectedShares]
  );
  const hasInvalidEditShareState = useMemo(
    () => hasInvalidSkillAttributeShares(editingShares),
    [editingShares]
  );

  const canSubmit = useMemo(
    () => name.trim().length >= 2 && !busy && !hasInvalidCreateShareState,
    [name, busy, hasInvalidCreateShareState]
  );

  const canEditSubmit = useMemo(
    () => editingName.trim().length >= 2 && !busy && !hasInvalidEditShareState,
    [editingName, busy, hasInvalidEditShareState]
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

  const setSelectedWithShares = (nextSelected: AttributeType[]) => {
    setSelected(nextSelected);
    setSelectedShares((current) => rebalanceSkillAttributeShares(nextSelected, current));
  };

  const setEditingWithShares = (nextSelected: AttributeType[]) => {
    setEditingAttributes(nextSelected);
    setEditingShares((current) => rebalanceSkillAttributeShares(nextSelected, current));
  };

  const toggleAttribute = (value: AttributeType) => {
    const nextSelected = selected.includes(value)
      ? selected.filter((item) => item !== value)
      : [...selected, value];

    setSelectedWithShares(nextSelected);
  };

  const toggleEditingAttribute = (value: AttributeType) => {
    const nextSelected = editingAttributes.includes(value)
      ? editingAttributes.filter((item) => item !== value)
      : [...editingAttributes, value];

    setEditingWithShares(nextSelected);
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
        attributes: selected,
        attributeShares: selectedShares.map((share) => ({
          attributeType: share.attributeType,
          percent: share.percent
        }))
      });
      setSkills((current) =>
        [...current, response].sort((a, b) => a.name.localeCompare(b.name))
      );
      setName("");
      setSelected([]);
      setSelectedShares([]);
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

    const attributes = skill.attributes as AttributeType[];
    setEditingAttributes(attributes);

    const responseShares = skill.attributeShares
      .map((share) => {
        if (!attributes.includes(share.attributeType as AttributeType)) {
          return null;
        }

        return {
          attributeType: share.attributeType as AttributeType,
          percent: share.percent
        } satisfies SkillAttributeShare;
      })
      .filter((item): item is SkillAttributeShare => item !== null);

    setEditingShares(
      rebalanceSkillAttributeShares(
        attributes,
        responseShares.length > 0 ? responseShares : []
      )
    );
  };

  const cancelEditing = () => {
    setEditingSkillId(null);
    setEditingName("");
    setEditingAttributes([]);
    setEditingShares([]);
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
        attributes: editingAttributes,
        attributeShares: editingShares.map((share) => ({
          attributeType: share.attributeType,
          percent: share.percent
        }))
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
        <p className="muted">Создайте личный навык и свяжите его с характеристиками.</p>
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

          {selectedShares.length > 1 && (
            <div className="field">
              <span>Распределение вклада по характеристикам</span>
              <SkillAttributeDistributionSlider
                shares={selectedShares}
                onChange={setSelectedShares}
              />
              <span className="muted">
                Чем больше процент, тем больше очков характеристика получит от навыка.
              </span>
            </div>
          )}

          {hasInvalidCreateShareState && (
            <div className="error">Сумма процентов распределения должна быть ровно 100%.</div>
          )}
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
                      skill.attributes.map((attr) => {
                        const share = skill.attributeShares.find(
                          (item) => item.attributeType === attr
                        );
                        return (
                          <span key={attr} className="pill attribute-pill" data-attribute={attr}>
                            {ATTRIBUTE_LABELS[attr] ?? attr}
                            {share ? ` ${share.percent}%` : ""}
                          </span>
                        );
                      })
                    )}
                  </div>
                </div>
                <div className="list-actions">
                  <button className="ghost" type="button" onClick={() => startEditing(skill)}>
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

            {editingShares.length > 1 && (
              <div className="field">
                <span>Распределение вклада по характеристикам</span>
                <SkillAttributeDistributionSlider
                  shares={editingShares}
                  onChange={setEditingShares}
                />
                <span className="muted">
                  Чем больше процент, тем больше очков характеристика получит от навыка.
                </span>
              </div>
            )}

            {hasInvalidEditShareState && (
              <div className="error">Сумма процентов распределения должна быть ровно 100%.</div>
            )}
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
