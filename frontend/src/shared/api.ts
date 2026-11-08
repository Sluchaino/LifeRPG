export class ApiError extends Error {
  status: number;
  details: unknown;

  constructor(status: number, message: string, details: unknown) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

const API_BASE = import.meta.env.VITE_API_BASE ?? "/api";

function extractErrorMessage(payload: unknown, fallback: string) {
  if (typeof payload === "string" && payload.trim().length > 0) {
    return payload;
  }

  if (!payload || typeof payload !== "object") {
    return fallback;
  }

  if ("error" in payload && typeof payload.error === "string" && payload.error.trim()) {
    return payload.error;
  }

  if ("detail" in payload && typeof payload.detail === "string" && payload.detail.trim()) {
    return payload.detail;
  }

  if ("title" in payload && typeof payload.title === "string" && payload.title.trim()) {
    return payload.title;
  }

  if ("errors" in payload && payload.errors && typeof payload.errors === "object") {
    const entries = Object.values(payload.errors as Record<string, unknown>);
    const firstArray = entries.find((value) => Array.isArray(value)) as unknown[] | undefined;
    const firstMessage = firstArray?.find((value) => typeof value === "string");
    if (typeof firstMessage === "string" && firstMessage.trim()) {
      return firstMessage;
    }
  }

  return fallback;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...(options.headers ?? {})
      },
      credentials: "include"
    });
  } catch (err) {
    throw new ApiError(
      0,
      "Не удалось подключиться к серверу. Проверьте, что backend запущен.",
      err
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const isJson =
    contentType.includes("application/json") ||
    contentType.includes("application/problem+json");

  let payload: unknown = null;
  if (isJson) {
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }
  } else {
    payload = await response.text();
  }

  if (!response.ok) {
    const fallback = response.statusText || "Ошибка запроса";
    const message = extractErrorMessage(payload, fallback);
    throw new ApiError(response.status, message, payload);
  }

  return payload as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: "POST",
      body: body ? JSON.stringify(body) : undefined
    }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: "PATCH",
      body: body ? JSON.stringify(body) : undefined
    }),
  delete: <T>(path: string) =>
    request<T>(path, {
      method: "DELETE"
    })
};
