import type { CoordinationChannel, CoordinationMessage } from './types';

export function asChannelList(payload: unknown): CoordinationChannel[] {
  if (Array.isArray(payload)) return payload as CoordinationChannel[];
  if (payload && typeof payload === 'object') {
    const record = payload as { channels?: CoordinationChannel[]; items?: CoordinationChannel[] };
    return record.channels ?? record.items ?? [];
  }
  return [];
}

export function asMessageList(payload: unknown): CoordinationMessage[] {
  if (Array.isArray(payload)) return payload as CoordinationMessage[];
  if (payload && typeof payload === 'object') {
    const record = payload as { messages?: CoordinationMessage[]; items?: CoordinationMessage[] };
    return record.messages ?? record.items ?? [];
  }
  return [];
}

export function messagePayloadText(message: CoordinationMessage): string {
  return message.allowedMetadataJson || message.payloadJson || '';
}

export function channelMessageCount(channels: CoordinationChannel[]): number {
  return channels.reduce((total, channel) => total + (channel.sequenceHighWatermark ?? 0), 0);
}

export function summarizeCoordinationPayload(raw: string, max = 180): string {
  if (!raw?.trim()) return '';
  try {
    return summarizeValue(JSON.parse(raw) as unknown, max);
  } catch {
    return raw.slice(0, max);
  }
}

export function prettyCoordinationPayload(raw: string): string {
  if (!raw?.trim()) return '';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

function summarizeValue(parsed: unknown, max: number): string {
  if (typeof parsed === 'string') return parsed.slice(0, max);
  if (Array.isArray(parsed)) return `${parsed.length} item${parsed.length === 1 ? '' : 's'}`;
  if (parsed && typeof parsed === 'object') {
    const record = parsed as Record<string, unknown>;
    const text = record.text ?? record.summary ?? record.body ?? record.title;
    if (typeof text === 'string' && text.trim()) return text.slice(0, max);
    const counts: string[] = [];
    for (const key of ['knowledge', 'items', 'relations', 'work', 'workItems', 'milestones', 'outline']) {
      const value = record[key];
      if (Array.isArray(value)) counts.push(`${value.length} ${key}`);
    }
    if (counts.length) return counts.join(' · ');
    const keys = Object.keys(record);
    if (keys.length) return `fields: ${keys.slice(0, 6).join(', ')}`;
  }
  try {
    return JSON.stringify(parsed).slice(0, max);
  } catch {
    return '';
  }
}
