import { getAccessToken, getActiveTenantId } from './session';

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly statusText: string,
    readonly path: string,
    readonly body?: string,
  ) {
    super(`Request to ${path} failed: ${status} ${statusText}`);
    this.name = 'ApiError';
  }
}

function authHeaders(): Record<string, string> {
  const token = getAccessToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...authHeaders(),
  };

  if (init?.body && !(init.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      ...headers,
      ...(init?.headers as Record<string, string> | undefined),
    },
  });

  if (!response.ok) {
    const body = await response.text().catch(() => undefined);
    throw new ApiError(response.status, response.statusText, path, body);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Multipart upload with progress (fetch cannot report upload progress). Rejects with ApiError like apiFetch. */
export function apiUpload<T>(path: string, form: FormData, onProgress?: (fraction: number) => void): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open('POST', `${baseUrl}${path}`);
    request.setRequestHeader('Accept', 'application/json');
    const token = getAccessToken();
    if (token) request.setRequestHeader('Authorization', `Bearer ${token}`);
    request.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress?.(event.loaded / event.total);
    };
    request.onerror = () => reject(new ApiError(0, 'Network error', path));
    request.onabort = () => reject(new ApiError(0, 'Upload cancelled', path));
    request.onload = () => {
      if (request.status >= 200 && request.status < 300) {
        try {
          resolve(JSON.parse(request.responseText) as T);
        } catch {
          resolve(undefined as T);
        }
      } else {
        reject(new ApiError(request.status, request.statusText, path, request.responseText));
      }
    };
    request.send(form);
  });
}

export function hasLiveConfig(): boolean {
  return Boolean(getActiveTenantId() && getAccessToken());
}
