// How numbers, dates and statuses are shown everywhere in the app (Indonesian style, WIB).

const rupiahFormat = new Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR', minimumFractionDigits: 0, maximumFractionDigits: 2 });
const numberFormat = new Intl.NumberFormat('id-ID', { maximumFractionDigits: 2 });
const compactFormat = new Intl.NumberFormat('id-ID', { notation: 'compact', maximumFractionDigits: 1 });
const dateTimeFormat = new Intl.DateTimeFormat('id-ID', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Jakarta' });
const dateFormat = new Intl.DateTimeFormat('id-ID', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
const dayFormat = new Intl.DateTimeFormat('id-ID', { weekday: 'short', timeZone: 'UTC' });
const longDateFormat = new Intl.DateTimeFormat('id-ID', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Asia/Jakarta' });

export const rupiah = (value) => rupiahFormat.format(value ?? 0);
export const number = (value) => numberFormat.format(value ?? 0);
export const compact = (value) => compactFormat.format(value ?? 0);

/** "30 Sep 2026, 21.37" in WIB, from an ISO timestamp. */
export const dateTime = (iso) => `${dateTimeFormat.format(new Date(iso))} WIB`;

/** "3 Okt 2026", from a date-only string "2026-10-03". */
export const date = (isoDate) => dateFormat.format(new Date(`${isoDate}T00:00:00Z`));
export const weekday = (isoDate) => dayFormat.format(new Date(`${isoDate}T00:00:00Z`));
export const longToday = () => longDateFormat.format(new Date());

/** Today's date in WIB as "yyyy-mm-dd". */
export function todayWib() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Jakarta' }).format(new Date());
}

export function addDays(isoDate, days) {
  const d = new Date(`${isoDate}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** "Hari ini", "Besok", "Terlambat 2 hari", or the date. */
export function dueText(isoDate) {
  if (!isoDate) return null;
  const diff = Math.round((new Date(`${isoDate}T00:00:00Z`) - new Date(`${todayWib()}T00:00:00Z`)) / 86_400_000);
  if (diff === 0) return 'Hari ini';
  if (diff === 1) return 'Besok';
  if (diff < 0) return `Terlambat ${-diff} hari`;
  return date(isoDate);
}

export const units = { Kg: 'kg', Pcs: 'pcs', M2: 'm²' };
export const unitLabel = (unit) => units[unit] ?? unit;

/** "2 kg" or "2 kg (ditagih min. 3 kg)". */
export function quantity(qty, billedQty, unit) {
  const text = `${number(qty)} ${unitLabel(unit)}`;
  return billedQty > qty ? `${text} (min. ${number(billedQty)} ${unitLabel(unit)})` : text;
}

export const statuses = {
  Baru: { label: 'Baru', icon: 'bi-inbox', badge: 'text-bg-primary' },
  Diproses: { label: 'Diproses', icon: 'bi-arrow-repeat', badge: 'text-bg-warning' },
  Selesai: { label: 'Siap diambil', icon: 'bi-check2-circle', badge: 'text-bg-success' },
  Diambil: { label: 'Diambil', icon: 'bi-bag-check', badge: 'text-bg-secondary' },
};
export const statusOrder = ['Baru', 'Diproses', 'Selesai', 'Diambil'];

export const payments = {
  Lunas: { label: 'Lunas', badge: 'text-bg-success', icon: 'bi-cash-coin' },
  BelumLunas: { label: 'Belum lunas', badge: 'text-bg-danger', icon: 'bi-hourglass-split' },
};

export function initials(name) {
  return (name ?? '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('') || '?';
}

/** Phone as WhatsApp wants it: 0812… → 62812…; "+62…" → "62…". */
export function waNumber(phone) {
  const digits = (phone ?? '').replace(/[^\d]/g, '');
  return digits.startsWith('0') ? `62${digits.slice(1)}` : digits;
}

export function waLink(phone, text) {
  return `https://wa.me/${waNumber(phone)}?text=${encodeURIComponent(text)}`;
}

/** "Rp 7.000 / kg · min. 3 kg" or "Rp 60.000 – Rp 150.000 / pcs". */
export function priceText(service) {
  const unit = unitLabel(service.unit);
  const price = service.maxPrice ? `${rupiah(service.price)} – ${rupiah(service.maxPrice)}` : rupiah(service.price);
  const min = service.minQty ? ` · min. ${number(service.minQty)} ${unit}` : '';
  return `${price} / ${unit}${min}`;
}
