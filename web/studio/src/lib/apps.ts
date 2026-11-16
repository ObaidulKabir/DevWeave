import { apiFetch } from './api';
import type { LifecycleProfile, Membership, Project, WhoAmI } from './types';

export function slugifyKey(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 48);
}

export async function ensureLifecycleProfile(tenantId: string): Promise<LifecycleProfile> {
  const existing = await apiFetch<LifecycleProfile[]>(`/tenants/${tenantId}/lifecycle-profiles`);
  const list = Array.isArray(existing) ? existing : [];
  if (list[0]) return list[0];
  return apiFetch<LifecycleProfile>(`/tenants/${tenantId}/lifecycle-profiles`, {
    method: 'POST',
    body: JSON.stringify({
      key: 'default',
      name: 'Default lifecycle',
      version: '1.0',
    }),
  });
}

export async function resolveActorId(fallback?: string | null): Promise<string> {
  if (fallback) return fallback;
  const me = await apiFetch<WhoAmI>('/auth/whoami');
  if (!me.principalId) throw new Error('Could not resolve your Owner principal. Sign in again.');
  return me.principalId;
}

export async function createTenantApplication(
  tenantId: string,
  input: { name: string; key?: string; description?: string },
  actorId?: string | null,
): Promise<Project> {
  const who = await resolveActorId(actorId);
  const name = input.name.trim();
  const key = (input.key?.trim() || slugifyKey(name));
  if (!name || !key) throw new Error('Application name and key are required.');

  const profile = await ensureLifecycleProfile(tenantId);
  return apiFetch<Project>(`/tenants/${tenantId}/projects`, {
    method: 'POST',
    body: JSON.stringify({
      name,
      key,
      description: input.description?.trim() || null,
      lifecycleProfileId: profile.id,
      createdBy: who,
    }),
  });
}

export async function ensureTenantMembership(tenantId: string, principalId: string): Promise<void> {
  const members = await apiFetch<Membership[]>(`/tenants/${tenantId}/members`);
  const list = Array.isArray(members) ? members : [];
  const existing = list.find((item) => item.principalId === principalId);
  if (existing?.status === 'Active') return;
  if (existing) {
    await apiFetch(`/tenants/${tenantId}/members/${existing.id}/activate`, { method: 'POST' });
    return;
  }
  const invited = await apiFetch<Membership>(`/tenants/${tenantId}/members`, {
    method: 'POST',
    body: JSON.stringify({ principalId, role: 'Member' }),
  });
  if (invited.status !== 'Active') {
    await apiFetch(`/tenants/${tenantId}/members/${invited.id}/activate`, { method: 'POST' });
  }
}

export async function ensureProjectMembership(
  tenantId: string,
  projectId: string,
  principalId: string,
): Promise<void> {
  try {
    await apiFetch(`/tenants/${tenantId}/projects/${projectId}/members`, {
      method: 'POST',
      body: JSON.stringify({ principalId, role: 'Member' }),
    });
  } catch {
    // Already a member (or soft conflict) — continue.
  }
}
