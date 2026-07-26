import type { NavigationResponse, ProblemDetails } from "./types";

const API_ROOT = "/api/identity-ui";
let antiforgeryPromise: Promise<{ requestToken: string; headerName: string }> | null = null;

export class IdentityApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(message);
  }
}

function currentReturnUrl() {
  return `${window.location.pathname}${window.location.search}`;
}

export async function identityFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_ROOT}${path}`, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      Accept: "application/json",
      ...init?.headers,
    },
  });

  if (response.status === 401) {
    const target = encodeURIComponent(currentReturnUrl());
    window.location.replace(`/Account/Login?returnUrl=${target}`);
    throw new IdentityApiError("Authentication is required.", response.status);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => undefined)) as ProblemDetails | undefined;
    const firstValidation = problem?.errors
      ? Object.values(problem.errors).flat()[0]
      : undefined;
    throw new IdentityApiError(
      firstValidation ?? problem?.detail ?? problem?.title ?? "The request could not be completed.",
      response.status,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export async function getAntiforgery() {
  antiforgeryPromise ??= identityFetch<{ requestToken: string; headerName: string }>("/antiforgery");
  return antiforgeryPromise;
}

export async function identityMutation<T>(path: string, method: "POST" | "DELETE", body?: unknown) {
  const antiforgery = await getAntiforgery();
  return identityFetch<T>(path, {
    method,
    headers: {
      "Content-Type": "application/json",
      [antiforgery.headerName]: antiforgery.requestToken,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export function applyNavigation(response: NavigationResponse) {
  window.location.assign(response.navigation.url);
}

export function queryParam(name: string) {
  if (typeof window === "undefined") return null;
  return new URLSearchParams(window.location.search).get(name);
}

export function formatDate(value: string | null) {
  if (!value) return "Never";
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
