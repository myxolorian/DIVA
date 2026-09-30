// Login with Supabase Auth, straight over its REST API (no extra library needed).
// The session (access + refresh token) is kept in localStorage so the owner stays logged in.
// Security note: everything on these pages is rendered with textContent, never innerHTML,
// so injected text cannot run code and read this storage (XSS).

const SESSION_KEY = 'diva.session';
let configPromise;

/** { supabaseUrl, supabasePublishableKey, devBypass } from the API (cached per page). */
export function getConfig() {
  configPromise ??= fetch('/api/public/config', { headers: { Accept: 'application/json' } })
    .then((r) => {
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      return r.json();
    })
    .catch((error) => {
      configPromise = undefined; // try again next time
      throw error;
    });
  return configPromise;
}

function readSession() {
  try {
    return JSON.parse(localStorage.getItem(SESSION_KEY) ?? 'null');
  } catch {
    return null;
  }
}

function saveSession(data) {
  const session = {
    accessToken: data.access_token,
    refreshToken: data.refresh_token,
    // expires_at is in seconds since 1970; fall back to expires_in when it is missing
    expiresAt: data.expires_at ?? Math.floor(Date.now() / 1000) + (data.expires_in ?? 3600),
    email: data.user?.email ?? null,
  };
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  return session;
}

export function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}

export function sessionEmail() {
  return readSession()?.email ?? null;
}

async function authRequest(path, body, accessToken) {
  const config = await getConfig();
  if (!config.supabaseUrl || !config.supabasePublishableKey) {
    throw new Error('Konfigurasi Supabase belum lengkap di server (Supabase:Url / Supabase:PublishableKey).');
  }
  const headers = { apikey: config.supabasePublishableKey, 'Content-Type': 'application/json' };
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  return fetch(`${config.supabaseUrl}/auth/v1/${path}`, { method: 'POST', headers, body: JSON.stringify(body ?? {}) });
}

/** Throws an Error with an Indonesian message when the login fails. */
export async function login(email, password) {
  let response;
  try {
    response = await authRequest('token?grant_type=password', { email, password });
  } catch (error) {
    throw new Error(error.message?.startsWith('Konfigurasi') ? error.message : 'Tidak bisa terhubung ke server login. Periksa koneksi internet.');
  }
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const code = data.error_code ?? data.error;
    if (code === 'invalid_credentials' || code === 'invalid_grant') throw new Error('Email atau password salah.');
    if (code === 'email_not_confirmed') throw new Error('Email belum dikonfirmasi di Supabase.');
    throw new Error(data.msg ?? data.error_description ?? 'Login gagal. Coba lagi.');
  }
  return saveSession(data);
}

let refreshing;

async function refresh(session) {
  refreshing ??= (async () => {
    try {
      const response = await authRequest('token?grant_type=refresh_token', { refresh_token: session.refreshToken });
      if (!response.ok) {
        clearSession();
        return null;
      }
      return saveSession(await response.json());
    } catch {
      return session; // offline: keep the old token, the API will say 401 if it expired
    } finally {
      refreshing = undefined;
    }
  })();
  return refreshing;
}

/** A valid access token, refreshed a minute before it expires; null when not logged in. */
export async function getAccessToken() {
  let session = readSession();
  if (!session?.accessToken) return null;
  if (session.expiresAt - Math.floor(Date.now() / 1000) < 60) {
    session = await refresh(session);
  }
  return session?.accessToken ?? null;
}

export async function logout() {
  const session = readSession();
  clearSession();
  if (session?.accessToken) {
    // Best effort: also end the session at Supabase. Leaving works even if this fails.
    authRequest('logout', {}, session.accessToken).catch(() => {});
  }
  location.href = '/login.html';
}

export function goToLogin() {
  const next = location.pathname + location.search;
  location.href = `/login.html?next=${encodeURIComponent(next)}`;
}
