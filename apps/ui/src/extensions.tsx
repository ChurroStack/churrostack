import { formatDistanceToNow as formatDistanceToNowFns, format } from 'date-fns';
import { DynamicIcon } from 'lucide-react/dynamic';

export const getBrowserTz = (): string => Intl.DateTimeFormat().resolvedOptions().timeZone;

export function formatDistanceToNow(rawDate: string | Date) {
  if (!rawDate || rawDate === '') {
    return '';
  }
  if (typeof rawDate === 'string') {
    rawDate = new Date(rawDate);
  }
  return formatDistanceToNowFns(rawDate as Date, {
    addSuffix: true
  });
}

export function formatDateTime(rawDate: string | Date, formatStr: string = 'PPPppp') {
  if (!rawDate || rawDate === '') {
    return '';
  }
  if (typeof rawDate === 'string') {
    rawDate = new Date(rawDate);
  }
  return format(rawDate as Date, formatStr);
}

export function formatDuration(rawDate: string | Date, rawDate2: string | Date, showMilliseconds?: boolean): string {
  if (typeof rawDate === 'string') {
    rawDate = new Date(rawDate);
  }
  if (typeof rawDate2 === 'string') {
    rawDate2 = new Date(rawDate2);
  }

  const durationMs = Math.abs((rawDate2 as Date).getTime() - (rawDate as Date).getTime());

  const hours = Math.floor(durationMs / (1000 * 60 * 60));
  const minutes = Math.floor((durationMs % (1000 * 60 * 60)) / (1000 * 60));
  const seconds = Math.floor((durationMs % (1000 * 60)) / 1000);
  const milliseconds = durationMs % 1000;

  const parts = [];
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);
  if (seconds > 0) parts.push(`${seconds}s`);
  if (milliseconds > 0 && showMilliseconds) parts.push(`${milliseconds}ms`);

  return parts.join(' ');
}

export function isNullOrWhiteSpace(str?: string) {
  return str === null || str === undefined || str.trim().length === 0;
}

export function capitalize(str: string): string {
  if (!str || str.length === 0) return '';
  return str.charAt(0).toUpperCase() + str.slice(1);
}

export function getInitials(fullName: string) {
  if (!fullName.trim()) {
    // Handle empty or whitespace-only input
    return 'UU';
  }

  // Split the name into parts by spaces
  const nameParts = fullName.trim().split(' ');

  // If there's only one part (e.g., "John"), use the first letter twice
  if (nameParts.length === 1) {
    const singleInitial = nameParts[0]![0]!.toUpperCase();
    return singleInitial + singleInitial;
  }

  // Get the first and last name parts
  const firstName = nameParts[0];
  const lastName = nameParts[1];

  // Extract the first letters and convert them to uppercase
  const initials = (firstName![0]! + lastName![0]).toUpperCase();

  return initials;
}

export function getLocalStorageValue<T>(key: string, defaultValue: T): T {
  try {
    const storedValue = localStorage.getItem(key);
    return storedValue ? (JSON.parse(storedValue) as T) : defaultValue;
  } catch (error) {
    console.error(`Error retrieving ${key} from localStorage:`, error);
    return defaultValue;
  }
}

export function setLocalStorageValue<T>(key: string, value: T): T {
  localStorage.setItem(key, JSON.stringify(value));
  return value;
}

export function removeLocalStorageValue(key: string) {
  localStorage.removeItem(key);
}

export function trimCharacters(str: string, chars: string): string {
  const regex = new RegExp(`^[${chars}]+|[${chars}]+$`, 'g');
  return str.replace(regex, '');
}

export function formatPercent(number: number) {
  if (isNaN(number)) return '0';
  return (number * 100).toFixed(2);
}

export function formatBytes(bytes: number, decimals = 2) {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

export function formatNumber(bytes: number, decimals = 2) {
  if (bytes === 0) return '0';

  const k = 1000;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['', 'K', 'M', 'B', 'T', 'P', 'E', 'Z', 'Y'];

  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

export function formatString(template: string, args: { [key: string]: string | number }): string {
  if (!template || typeof template !== 'string') {
    return '';
  }
  return template.replace(/\{(\w+)\}/g, (_, key) =>
    Object.prototype.hasOwnProperty.call(args, key) ? String(args[key]) : `{${key}}`
  );
}

export function isWebUrl(str: string): boolean {
  try {
    const url = new URL(str);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

export async function getHashFromString(value: string): Promise<string> {
  const msgBuffer = new TextEncoder().encode(value);
  const hashBuffer = await crypto.subtle.digest('SHA-1', msgBuffer);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  const hashHex = hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
  return hashHex;
}

/**
 * Fill missing days in an array of objects with a date and value.
 *
 * @param {Array<{date: string, value: number}>} data - Input array
 * @param {number} days - Number of past days to include (default 30)
 * @returns {Array<{date: string, value: number}>}
 */
export function fillMissingDays(
  data: { label: string; value: number }[],
  days: number = 30,
  valueField: string = 'value'
) {
  // Normalize input: map date -> value
  const map = new Map();
  data.forEach((item) => {
    const day = new Date(item.label);
    // Keep only YYYY-MM-DD (ignore time & timezone)
    const key = day.toISOString().split('T')[0];
    map.set(key, item.value);
  });

  const result = [];
  const today = new Date();

  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(today);
    d.setUTCHours(0, 0, 0, 0);
    d.setUTCDate(today.getUTCDate() - i);

    const key = d.toISOString().split('T')[0];
    result.push({
      label: key,
      [valueField]: map.get(key) ?? 0
    });
  }

  return result;
}

export function mergeByField(field: string, ...arrays: any[]) {
  const map = new Map();

  arrays.flat().forEach((obj) => {
    const key = obj[field];
    if (!map.has(key)) {
      map.set(key, { ...obj }); // copy first occurrence
    } else {
      // merge with existing object
      map.set(key, { ...map.get(key), ...obj });
    }
  });

  return Array.from(map.values());
}

type GroupResult<K, V> = { key: K; value: V };

export function groupReduce<T, K extends string | number | symbol, V>(
  items: T[],
  keySelector: (item: T) => K,
  valueSelector: (item: T) => V,
  aggregator: (acc: V, val: V) => V,
  initialValue: V
): GroupResult<K, V>[] {
  const map = new Map<K, V>();

  for (const item of items) {
    const key = keySelector(item);
    const value = valueSelector(item);

    if (!map.has(key)) {
      map.set(key, initialValue);
    }

    map.set(key, aggregator(map.get(key)!, value));
  }

  return Array.from(map, ([key, value]) => ({ key, value }));
}

export function distinctBy<T>(arr: T[], reducer: (item: T) => any): T[] {
  const seen = new Set<any>();
  return arr.filter((item) => {
    const value = reducer(item);
    if (seen.has(value)) {
      return false;
    }
    seen.add(value);
    return true;
  });
}

export function encodeRFC3986URI(label: string): string {
  // Codifica corchetes y puntos además de los caracteres RFC3986
  return encodeURIComponent(label).replace(/[!'()*\[\].]/g, (c) => '%' + c.charCodeAt(0).toString(16).toUpperCase());
}

export function decodeRFC3986URI(encoded: string): string {
  return decodeURIComponent(encoded);
}

export function getLastSegment(uri: string) {
  return (
    uri
      .split('/') // split by '/'
      .filter(Boolean) // remove empty segments (caused by '//' or trailing '/')
      .pop() || ''
  ); // get the last one (or '' if none)
}

export function sanitizeString(str: string) {
  return str
    .trim()
    .replace(/\s+/g, '-') // replace spaces with '-'
    .replace(/_+/g, '-') // replace underscores with '-'
    .replace(/[^a-zA-Z0-9-]/g, '') // keep only letters, numbers, and '-'
    .replace(/-+/g, '-') // collapse multiple '-'
    .replace(/^-|-$/g, '') // remove leading/trailing '-'
    .toLowerCase(); // make lowercase
}

export function renderIcon(icon: string | undefined, className?: string) {
  if (!icon) return <div className={className}></div>;
  if (icon.startsWith('lucide:')) {
    return <DynamicIcon name={icon.replace('lucide:', '') as any} className={className} />;
  }
  if (icon.startsWith('http:') || icon.startsWith('https:')) {
    return <img src={icon} className={className} />;
  }
  if (icon.startsWith('/')) {
    // Root-relative path to a static asset served from the PWA public folder.
    return <img src={icon} className={className} />;
  }
  return <div className={className}></div>;
}

export function parseSize(sizeStr: string) {
  if (typeof sizeStr !== 'string') return NaN;

  // Normalize the string: remove spaces, lowercase
  const normalized = sizeStr.trim().toLowerCase();

  // Match number + unit (e.g., "2gi", "35mb")
  const match = normalized.match(/^([\d.]+)\s*(\w+)$/);
  if (!match) return NaN;

  const value = parseFloat(match[1]);
  const unit = match[2];

  // Define unit multipliers
  const units = {
    b: 1,
    kb: 1e3,
    mb: 1e6,
    gb: 1e9,
    tb: 1e12,
    pb: 1e15,
    eb: 1e18,
    kib: 1024,
    mib: 1024 ** 2,
    gib: 1024 ** 3,
    tib: 1024 ** 4,
    pib: 1024 ** 5,
    eib: 1024 ** 6,
    ki: 1024,
    mi: 1024 ** 2,
    gi: 1024 ** 3,
    ti: 1024 ** 4,
    pi: 1024 ** 5,
    ei: 1024 ** 6
  };

  const multiplier = (units as any)[unit];
  if (!multiplier) return NaN;

  return value * multiplier;
}

export function randomString(length = 6) {
  return Math.random()
    .toString(36)
    .substring(2, 2 + length);
}

export function parseCpu(cpu: string): number {
  if (!cpu) throw new Error('CPU limit is required');

  cpu = cpu.trim().toLowerCase();

  if (cpu.endsWith('m')) {
    // Millicores: convert to cores
    const millicores = parseFloat(cpu.slice(0, -1));
    if (isNaN(millicores)) throw new Error(`Invalid CPU limit: ${cpu}`);
    return millicores / 1000;
  } else {
    // Cores: parse directly
    const cores = parseFloat(cpu);
    if (isNaN(cores)) throw new Error(`Invalid CPU limit: ${cpu}`);
    return cores;
  }
}
