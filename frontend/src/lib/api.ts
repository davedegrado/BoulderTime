import { env } from "@/config/env";
import { supabase } from "@/lib/supabase";
import { ApiError, defaultMessage, toApiError } from "@/lib/apiError";
import { activeLanguage } from "@/i18n/i18n";

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

interface RequestOptions {
  body?: unknown;
  signal?: AbortSignal;
  /** Send without an Authorization header even when signed in. */
  anonymous?: boolean;
}

async function request<T>(method: Method, path: string, options: RequestOptions = {}): Promise<T> {
  // The server answers (and writes notifications) in the language the person is using.
  const headers: Record<string, string> = { Accept: "application/json", "Accept-Language": activeLanguage() };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";

  if (!options.anonymous) {
    const { data } = await supabase.auth.getSession();
    const token = data.session?.access_token;
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(`${env.apiBaseUrl}${path}`, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });
  } catch (err) {
    if (err instanceof DOMException && err.name === "AbortError") throw err;
    throw new ApiError(0, null, defaultMessage(0));
  }

  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, o?: RequestOptions) => request<T>("GET", path, o),
  post: <T>(path: string, body?: unknown, o?: RequestOptions) => request<T>("POST", path, { ...o, body }),
  put: <T>(path: string, body?: unknown, o?: RequestOptions) => request<T>("PUT", path, { ...o, body }),
  patch: <T>(path: string, body?: unknown, o?: RequestOptions) => request<T>("PATCH", path, { ...o, body }),
  delete: <T>(path: string, o?: RequestOptions) => request<T>("DELETE", path, o),
};
