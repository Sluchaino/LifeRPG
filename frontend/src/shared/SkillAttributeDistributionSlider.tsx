import { useEffect, useMemo, useRef, useState } from "react";
import {
  ATTRIBUTE_LABELS,
  AttributeType,
  getAttributeSoftColor,
  getAttributeStrongColor
} from "./attributes";
import { SkillAttributeShare } from "./skillAttributeShares";

type SkillAttributeDistributionSliderProps = {
  shares: SkillAttributeShare[];
  onChange: (shares: SkillAttributeShare[]) => void;
};

const MIN_SHARE_PERCENT = 1;
const TOTAL_PERCENT = 100;

function toBoundaries(shares: SkillAttributeShare[]): number[] {
  const boundaries: number[] = [];
  let cumulative = 0;

  for (let index = 0; index < shares.length - 1; index += 1) {
    cumulative += shares[index].percent;
    boundaries.push(cumulative);
  }

  return boundaries;
}

function toShares(
  attributes: AttributeType[],
  boundaries: number[]
): SkillAttributeShare[] {
  const normalizedBoundaries = [...boundaries]
    .map((value) => Math.max(0, Math.min(TOTAL_PERCENT, Math.round(value))))
    .sort((left, right) => left - right);

  const values: number[] = [];
  let previous = 0;

  normalizedBoundaries.forEach((boundary) => {
    values.push(boundary - previous);
    previous = boundary;
  });

  values.push(TOTAL_PERCENT - previous);

  return attributes.map((attributeType, index) => ({
    attributeType,
    percent: values[index] ?? 0
  }));
}

function buildSummaryStyle(shares: SkillAttributeShare[]): string {
  if (shares.length === 0) {
    return "transparent";
  }

  if (shares.length === 1) {
    return getAttributeSoftColor(shares[0].attributeType);
  }

  const segments: string[] = [];
  let cursor = 0;
  shares.forEach((share) => {
    const start = cursor;
    const end = cursor + share.percent;
    const color = getAttributeSoftColor(share.attributeType);
    segments.push(`${color} ${start}%`);
    segments.push(`${color} ${end}%`);
    cursor = end;
  });

  return `linear-gradient(90deg, ${segments.join(", ")})`;
}

function shortLabel(value: string): string {
  const compact = value.replace(/\s+/g, "").trim();
  if (compact.length <= 2) {
    return compact.toUpperCase();
  }
  return compact.slice(0, 2).toUpperCase();
}

export function SkillAttributeDistributionSlider({
  shares,
  onChange
}: SkillAttributeDistributionSliderProps) {
  const trackRef = useRef<HTMLDivElement | null>(null);
  const [draggingIndex, setDraggingIndex] = useState<number | null>(null);
  const [boundaries, setBoundaries] = useState<number[]>(() => toBoundaries(shares));

  const attributes = useMemo(
    () => shares.map((share) => share.attributeType),
    [shares]
  );

  const sharesKey = useMemo(
    () => shares.map((share) => `${share.attributeType}:${share.percent}`).join("|"),
    [shares]
  );

  useEffect(() => {
    setBoundaries(toBoundaries(shares));
  }, [sharesKey]);

  useEffect(() => {
    if (draggingIndex === null) {
      return;
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (!trackRef.current) {
        return;
      }

      const rect = trackRef.current.getBoundingClientRect();
      if (rect.width <= 0) {
        return;
      }

      const currentBoundaries = toBoundaries(shares);
      const previousBoundary = draggingIndex === 0 ? 0 : currentBoundaries[draggingIndex - 1];
      const nextBoundary =
        draggingIndex === currentBoundaries.length - 1
          ? TOTAL_PERCENT
          : currentBoundaries[draggingIndex + 1];

      const minValue = previousBoundary + MIN_SHARE_PERCENT;
      const maxValue = nextBoundary - MIN_SHARE_PERCENT;

      const raw = ((event.clientX - rect.left) / rect.width) * TOTAL_PERCENT;
      const normalized = Math.round(raw);
      const clamped = Math.min(maxValue, Math.max(minValue, normalized));

      if (Number.isNaN(clamped)) {
        return;
      }

      const nextBoundaries = [...currentBoundaries];
      if (nextBoundaries[draggingIndex] === clamped) {
        return;
      }

      nextBoundaries[draggingIndex] = clamped;
      setBoundaries(nextBoundaries);
      onChange(toShares(attributes, nextBoundaries));
    };

    const stopDragging = () => {
      setDraggingIndex(null);
    };

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", stopDragging);
    window.addEventListener("pointercancel", stopDragging);

    return () => {
      window.removeEventListener("pointermove", handlePointerMove);
      window.removeEventListener("pointerup", stopDragging);
      window.removeEventListener("pointercancel", stopDragging);
    };
  }, [attributes, draggingIndex, onChange, shares]);

  if (shares.length <= 1) {
    return null;
  }

  let start = 0;
  const segments = shares.map((share) => {
    const segment = {
      attributeType: share.attributeType,
      start,
      width: share.percent
    };
    start += share.percent;
    return segment;
  });

  return (
    <div className="skill-share-editor">
      <div className="skill-share-track" ref={trackRef} style={{ background: buildSummaryStyle(shares) }}>
        {segments.map((segment) => (
          <div
            key={segment.attributeType}
            className="skill-share-segment"
            data-attribute={segment.attributeType}
            style={{
              left: `${segment.start}%`,
              width: `${segment.width}%`
            }}
          />
        ))}

        {boundaries.map((boundary, index) => {
          const markerAttribute = shares[index + 1]?.attributeType ?? shares[index].attributeType;
          const markerLabel = ATTRIBUTE_LABELS[markerAttribute] ?? markerAttribute;

          return (
            <button
              key={`${markerAttribute}-${index}`}
              type="button"
              className="skill-share-handle"
              data-attribute={markerAttribute}
              style={{
                left: `${boundary}%`,
                borderColor: getAttributeStrongColor(markerAttribute)
              }}
              title={`Сдвинуть границу: ${markerLabel}`}
              onPointerDown={(event) => {
                event.preventDefault();
                setDraggingIndex(index);
              }}
            >
              {shortLabel(markerLabel)}
            </button>
          );
        })}
      </div>

      <div className="skill-share-summary">
        {shares.map((share) => (
          <span
            key={share.attributeType}
            className="pill attribute-pill"
            data-attribute={share.attributeType}
          >
            {ATTRIBUTE_LABELS[share.attributeType]}: {share.percent}%
          </span>
        ))}
      </div>
    </div>
  );
}
