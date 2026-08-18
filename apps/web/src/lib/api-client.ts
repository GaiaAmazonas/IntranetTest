const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

export async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const headers = new Headers(options?.headers);
  if (!(options?.body instanceof FormData) && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  const response = await fetch(`${apiUrl}${path}`, { credentials: "include", ...options,
    headers });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { code?: string; detail?: string; errors?: Record<string, string[]> } | null;
    if (response.status === 401) {
      if (problem?.code === "reauth_required") window.dispatchEvent(new CustomEvent("gaia:reauth-required"));
      else window.location.href = "/";
    }
    throw new Error(problem?.detail ?? Object.values(problem?.errors ?? {}).flat()[0] ?? "No fue posible completar la operación.");
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

export function startLogin(returnUrl = window.location.href) {
  window.location.href = `${apiUrl}/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}
