import { AttributeType } from "./attributes";

export type SkillAttributeShare = {
  attributeType: AttributeType;
  percent: number;
};

const TOTAL_PERCENT = 100;

function normalizeAttributes(attributes: AttributeType[]): AttributeType[] {
  const unique: AttributeType[] = [];
  attributes.forEach((attribute) => {
    if (!unique.includes(attribute)) {
      unique.push(attribute);
    }
  });
  return unique;
}

export function createEqualSkillAttributeShares(
  attributes: AttributeType[]
): SkillAttributeShare[] {
  const normalized = normalizeAttributes(attributes);
  if (normalized.length === 0) {
    return [];
  }

  if (normalized.length === 1) {
    return [{ attributeType: normalized[0], percent: TOTAL_PERCENT }];
  }

  const base = Math.floor(TOTAL_PERCENT / normalized.length);
  let remainder = TOTAL_PERCENT - base * normalized.length;

  return normalized.map((attributeType) => {
    const extra = remainder > 0 ? 1 : 0;
    remainder = Math.max(0, remainder - 1);
    return {
      attributeType,
      percent: base + extra
    };
  });
}

export function rebalanceSkillAttributeShares(
  attributes: AttributeType[],
  previousShares: SkillAttributeShare[]
): SkillAttributeShare[] {
  const normalized = normalizeAttributes(attributes);
  if (normalized.length === 0) {
    return [];
  }

  if (normalized.length === 1) {
    return [{ attributeType: normalized[0], percent: TOTAL_PERCENT }];
  }

  const previousByType = new Map(
    previousShares.map((share) => [share.attributeType, share.percent])
  );

  const hasNewAttribute = normalized.some((attribute) => !previousByType.has(attribute));
  if (hasNewAttribute) {
    return createEqualSkillAttributeShares(normalized);
  }

  const keptShares = normalized.map((attributeType) => ({
    attributeType,
    percent: Math.max(0, previousByType.get(attributeType) ?? 0)
  }));

  const total = keptShares.reduce((sum, share) => sum + share.percent, 0);
  if (total <= 0) {
    return createEqualSkillAttributeShares(normalized);
  }

  const scaled = keptShares.map((share) => ({
    attributeType: share.attributeType,
    raw: (share.percent / total) * TOTAL_PERCENT
  }));

  const rounded = scaled.map((item) => ({
    attributeType: item.attributeType,
    percent: Math.floor(item.raw),
    fraction: item.raw - Math.floor(item.raw)
  }));

  let remaining = TOTAL_PERCENT - rounded.reduce((sum, item) => sum + item.percent, 0);

  rounded
    .sort((left, right) => right.fraction - left.fraction)
    .forEach((item) => {
      if (remaining <= 0) {
        return;
      }

      item.percent += 1;
      remaining -= 1;
    });

  const byType = new Map(rounded.map((item) => [item.attributeType, item.percent]));
  return normalized.map((attributeType) => ({
    attributeType,
    percent: byType.get(attributeType) ?? 0
  }));
}

export function hasInvalidSkillAttributeShares(shares: SkillAttributeShare[]): boolean {
  if (shares.length === 0) {
    return false;
  }

  if (shares.some((share) => share.percent <= 0 || share.percent > TOTAL_PERCENT)) {
    return true;
  }

  const total = shares.reduce((sum, share) => sum + share.percent, 0);
  return total !== TOTAL_PERCENT;
}
