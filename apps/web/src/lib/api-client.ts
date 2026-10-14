const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
export const loginTransitionKey = "gaia-login-transition";
let csrfTokenPromise: Promise<string> | null = null;

function isUnsafe(method?: string) {
  return !["GET", "HEAD", "OPTIONS", "TRACE"].includes((method ?? "GET").toUpperCase());
}

async function csrfToken() {
  if (!csrfTokenPromise) csrfTokenPromise = fetch(`${apiUrl}/api/auth/csrf`, { credentials: "include" })
    .then(async response => {
      if (!response.ok) throw new Error("No fue posible iniciar la operación segura.");
      const result = await response.json() as { token?: string };
      if (!result.token) throw new Error("El servidor no entregó el token de seguridad.");
      return result.token;
    })
    .catch(error => { csrfTokenPromise = null; throw error; });
  return csrfTokenPromise;
}

export async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const headers = new Headers(options?.headers);
  if (!(options?.body instanceof FormData) && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  if (isUnsafe(options?.method)) headers.set("X-CSRF-TOKEN", await csrfToken());
  const response = await fetch(`${apiUrl}${path}`, { credentials: "include", ...options,
    headers });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { code?: string; detail?: string; errors?: Record<string, string[]> } | null;
    if (response.status === 401) {
      if (problem?.code === "reauth_required") window.dispatchEvent(new CustomEvent("gaia:reauth-required"));
      else window.location.href = "/";
    }
    const fallback = response.status === 403
      ? "Tu cuenta está autenticada, pero no está autorizada para ejecutar esta operación."
      : "No fue posible completar la operación.";
    throw new Error(problem?.detail ?? Object.values(problem?.errors ?? {}).flat()[0] ?? fallback);
  }
  if (response.status === 204) return undefined as T;
  const body = await response.text();
  if (!body.trim()) return undefined as T;
  return JSON.parse(body) as T;
}

export function startLogin(returnUrl = window.location.href) {
  try {
    window.sessionStorage.setItem(loginTransitionKey, Date.now().toString());
  } catch {
    // El inicio de sesión debe continuar aunque el almacenamiento del navegador no esté disponible.
  }
  window.location.href = `${apiUrl}/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

export async function startLogout(returnUrl = `${window.location.origin}/?logout=success`) {
  const token = await csrfToken();
  const form = document.createElement("form");
  form.method = "POST";
  form.action = `${apiUrl}/api/auth/logout?returnUrl=${encodeURIComponent(returnUrl)}`;
  const field = document.createElement("input");
  field.type = "hidden";
  field.name = "__RequestVerificationToken";
  field.value = token;
  form.append(field);
  document.body.append(form);
  form.submit();
}
