import type { Track } from '../../types/track';
export type { Track } from '../../types/track';


export class ApiError extends Error {
  constructor(public status: number, public code: string) { super(code); }
}

const prefix = '/api/v1/diagnostics';
let csrfToken = '';
async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(prefix + path, {
    ...options, credentials: 'same-origin',
    headers: { ...(options.method === 'POST' ? { 'X-CSRF-Token': csrfToken } : {}), ...options.headers },
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.code ?? 'requestFailed');
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
export const api = {
  async session() {
    const result = await request<{ csrfToken: string; expiresAt: string }>('/session');
    csrfToken = result.csrfToken;
    return result;
  },
  async signIn(operatorKey: string) {
    csrfToken = (await request<{ csrfToken: string }>('/bootstrap')).csrfToken;
    await request('/session', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ operatorKey }) });
    return api.session();
  },
  async logout() { await request('/logout', { method: 'POST' }); csrfToken = ''; },
  async list() { return (await request<{ items: Track[] }>('/fixtures')).items; },
  track(id: string) { return request<Track>(`/fixtures/${encodeURIComponent(id)}`); },
  upload(file: File, signal: AbortSignal) {
    return request<{ track: Track; duplicate: boolean }>('/fixtures', {
      method: 'POST', body: file, signal,
      headers: { 'Content-Type': 'audio/mpeg', 'X-File-Name': encodeURIComponent(file.name), 'X-File-Size': String(file.size) },
    });
  },
  streamUrl(id: string) { return `${prefix}/fixtures/${encodeURIComponent(id)}/stream`; },
};
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const messages: Record<string, string> = {
      signInRequired: 'Your diagnostic session expired. Sign in again.',
      csrfRejected: 'The session could not be verified. Reload and sign in again.',
      invalidMp3: 'The file did not pass the local MP3 structure check.',
      fileTooLarge: 'Choose one MP3 no larger than 50 MiB.',
      transferIncomplete: 'The transfer was incomplete. Retry the original file.',
      quotaExceeded: 'The local experiment storage limit has been reached.',
      storageUnavailable: 'Local storage is unavailable. Retry after checking the server.',
      trackUnavailable: 'This fixture is unavailable. Upload it again after a server restart.',
    };
    return messages[error.code] ?? `Request failed (${error.status}). Retry after checking the server.`;
  }
  return 'Cannot reach the local server. Check that it is running, then retry.';
}
