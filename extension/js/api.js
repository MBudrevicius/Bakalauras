export const API_BASE = "http://clearsource.runasp.net";

export async function fetchApi(path, options = {}) {
  const { headers: customHeaders, ...rest } = options;
  const res = await fetch(`${API_BASE}${path}`, {
    ...rest,
    headers: { "Content-Type": "application/json", ...customHeaders },
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || `Server error ${res.status}`);
  }
  return res.json();
}
