import { t } from "@/i18n/i18n";
/** Mirrors the RFC 7807 ProblemDetails shape every API error uses. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly traceId?: string;

  constructor(status: number, problem: ProblemDetails | null, fallback: string) {
    super(problem?.detail || problem?.title || fallback);
    this.name = "ApiError";
    this.status = status;
    this.code = problem?.code;
    this.traceId = problem?.traceId;
    this.fieldErrors = normalizeFieldErrors(problem?.errors);
  }

  get isUnauthorized() { return this.status === 401; }
  get isForbidden() { return this.status === 403; }
  get isNotFound() { return this.status === 404; }
  get isValidation() { return this.status === 400 || this.status === 422; }

  fieldError(field: string): string | undefined {
    return this.fieldErrors[field.toLowerCase()]?.[0];
  }
}

/** ASP.NET may return PascalCase or camelCase keys; normalize to lowercase. */
function normalizeFieldErrors(errors?: Record<string, string[]>): Record<string, string[]> {
  const out: Record<string, string[]> = {};
  if (!errors) return out;
  for (const [key, messages] of Object.entries(errors)) {
    out[key.replace(/^\$\./, "").toLowerCase()] = messages;
  }
  return out;
}

export async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | null = null;
  try {
    const text = await response.text();
    problem = text ? (JSON.parse(text) as ProblemDetails) : null;
  } catch {
    problem = null;
  }
  return new ApiError(response.status, problem, defaultMessage(response.status));
}

export function defaultMessage(status: number): string {
  if (status === 0) return t("Can't reach BoulderTime. Check your connection and try again.");
  if (status === 401) return t("Your session has ended. Sign in again to continue.");
  if (status === 403) return t("You don't have permission to do that.");
  if (status === 404) return t("That page or item doesn't exist.");
  if (status >= 500) return t("Something went wrong on our side. Try again in a moment.");
  return t("The request couldn't be completed.");
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error && error.message) return error.message;
  return t("Something went wrong.");
}
