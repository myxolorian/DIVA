// Small wrapper around fetch() for the DIVA API: adds the login token, sends/reads JSON,
// and turns error responses (ProblemDetails) into an ApiError with a readable message.
import { getAccessToken, goToLogin } from './auth.js';

export class ApiError extends Error {
  constructor(status, problem) {
    super(problem?.title ?? `Terjadi kesalahan (HTTP ${status}).`);
    this.status = status;
    this.problem = problem ?? {};
  }

  /** { fieldName: ["message", ...] } for 400 validation errors. */
  get fieldErrors() {
    return this.problem.errors ?? {};
  }
}

export async function api(path, { method = 'GET', body, query } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const token = await getAccessToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  let url = path;
  if (query) {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') params.set(key, value);
    }
    const qs = params.toString();
    if (qs) url += `?${qs}`;
  }

  let response;
  try {
    response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, { title: 'Tidak bisa terhubung ke server. Periksa koneksi lalu coba lagi.' });
  }

  if (response.status === 401) {
    goToLogin();
    throw new ApiError(401, { title: 'Sesi berakhir. Silakan login lagi.' });
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    if (response.status === 400 && problem && !problem.errors && problem.title?.includes('Exception')) {
      // Development error page for malformed JSON: show something friendlier.
      problem.title = 'Data yang dikirim tidak valid.';
    }
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) return null;
  return response.json();
}
