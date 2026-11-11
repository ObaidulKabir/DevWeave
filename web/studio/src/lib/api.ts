const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';
const authToken = import.meta.env.VITE_API_TOKEN ?? '';

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

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
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
    if (authToken) request.setRequestHeader('Authorization', `Bearer ${authToken}`);
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
  return Boolean(import.meta.env.VITE_TENANT_ID && import.meta.env.VITE_API_TOKEN);
}
