import { api } from '../api.js';
import { startPage } from '../layout.js';
import { dailyChart } from '../chart.js';
import { el, fill, icon, $, emptyState, pageError } from '../ui.js';
import { rupiah, number, date, dateTime, weekday, todayWib, addDays } from '../format.js';

// "Pemasukan" = money that came in: an order counts on the day it was marked paid (paidAt).
const MAX_CHART_DAYS = 62;
const content = $('#content');
const customForm = $('#custom');
const today = todayWib();

function monthStart(isoDate) {
  return `${isoDate.slice(0, 8)}01`;
}

const thisMonth = monthStart(today);
const lastMonthEnd = addDays(thisMonth, -1);
const presets = [
  { key: 'today', label: 'Hari ini', from: today, to: today },
  { key: '7d', label: '7 hari', from: addDays(today, -6), to: today },
  { key: 'month', label: 'Bulan ini', from: thisMonth, to: today },
  { key: 'last-month', label: 'Bulan lalu', from: monthStart(lastMonthEnd), to: lastMonthEnd },
];

// The chosen chip is remembered by its key, not by its dates: on the 1st of a month
// "Hari ini" and "Bulan ini" cover the same day but only one of them was picked.
// URL: ?period=month for a chip, ?from=…&to=… for dates picked by hand.
const params = new URLSearchParams(location.search);
const state = { period: 'month', from: thisMonth, to: today };
const fromUrl = presets.find((p) => p.key === params.get('period'));
if (fromUrl) {
  Object.assign(state, { period: fromUrl.key, from: fromUrl.from, to: fromUrl.to });
} else if (params.get('from') && params.get('to')) {
  Object.assign(state, { period: 'custom', from: params.get('from'), to: params.get('to') });
}

function renderPeriods() {
  const chip = (label, active, onclick) =>
    el('button', { type: 'button', class: `chip${active ? ' active' : ''}`, 'aria-pressed': String(active), onclick }, label);
  $('#periods').replaceChildren(
    ...presets.map((p) => chip(p.label, state.period === p.key, () => {
      Object.assign(state, { period: p.key, from: p.from, to: p.to });
      load();
    })),
    chip('Pilih tanggal', state.period === 'custom', () => {
      state.period = 'custom';
      renderPeriods();
      customForm.from.focus();
    }));
  customForm.classList.toggle('d-none', state.period !== 'custom');
  customForm.from.value = state.from;
  customForm.to.value = state.to;
  customForm.from.max = customForm.to.max = today;
}

const periodText = (from, to) => (from === to ? date(from) : `${date(from)} – ${date(to)}`);

function paidRow(order) {
  return el('a', { class: 'list-row', href: `/order.html?id=${order.id}` },
    el('span', { class: 'avatar' }, icon('bi-cash-coin')),
    el('span', { class: 'grow' },
      el('span', { class: 'title d-block' }, order.customerName),
      el('span', { class: 'sub d-block' }, `${order.orderNumber} · lunas ${dateTime(order.paidAt)}`)),
    el('span', { class: 'fw-bold num' }, rupiah(order.total)));
}

function chartLabel(days) {
  // A week reads best as weekdays; a month as day numbers.
  if (days.length <= 7) return (point) => (point.date === today ? 'Hari ini' : weekday(point.date));
  return (point) => String(Number(point.date.slice(8)));
}

function render(report) {
  $('#period-label').textContent = periodText(report.from, report.to);
  const days = report.days.length;
  const bestDay = report.days.reduce((best, d) => (d.total > best.total ? d : best), report.days[0]);
  const showChart = days > 1 && days <= MAX_CHART_DAYS && report.orders > 0;

  fill(content,
    el('div', { class: 'row g-3' },
      el('div', { class: 'col-12 col-md-6' },
        el('div', { class: 'stat stat-hero' },
          el('div', { class: 'label' }, 'Total pemasukan'),
          el('div', { class: 'value num' }, rupiah(report.total)),
          el('div', { class: 'hint' }, `${number(report.orders)} order lunas`))),
      el('div', { class: 'col-6 col-md-3' },
        el('div', { class: 'stat' },
          el('div', { class: 'label' }, 'Rata-rata per hari'),
          el('div', { class: 'value num' }, rupiah(Math.round(report.total / days))),
          el('div', { class: 'hint' }, `${number(days)} hari`))),
      el('div', { class: 'col-6 col-md-3' },
        el('div', { class: 'stat' },
          el('div', { class: 'label' }, 'Hari terbaik'),
          el('div', { class: 'value num' }, report.orders ? rupiah(bestDay.total) : '–'),
          el('div', { class: 'hint' }, report.orders ? date(bestDay.date) : 'belum ada')))),

    showChart ? el('h2', { class: 'section-title' }, 'Pemasukan per hari') : null,
    showChart
      ? el('div', { class: 'card-soft p-3' }, dailyChart(report.days, {
          highlight: 'max',
          label: chartLabel(report.days),
          title: `Pemasukan per hari, ${periodText(report.from, report.to)}`,
          caption: 'Pemasukan per hari',
        }))
      : null,

    el('h2', { class: 'section-title' }, 'Order lunas'),
    report.items.length
      ? el('div', { class: 'list-card' }, report.items.map(paidRow))
      : el('div', { class: 'card-soft' }, emptyState('bi-cash-stack', 'Belum ada pemasukan di periode ini',
          'Order masuk ke laporan saat ditandai Lunas.')));
}

let requestId = 0;
async function load() {
  renderPeriods();
  history.replaceState(null, '', state.period === 'custom' ? `?from=${state.from}&to=${state.to}` : `?period=${state.period}`);
  $('#period-label').textContent = periodText(state.from, state.to);
  const id = ++requestId;
  $('#custom-error').textContent = '';
  content.replaceChildren(el('div', { class: 'skeleton', style: 'height:7rem' }));
  try {
    const report = await api('/api/reports/income', { query: { from: state.from, to: state.to } });
    if (id === requestId) render(report);
  } catch (error) {
    if (id !== requestId) return;
    // A period the server refuses (e.g. longer than a year): explain it next to the date fields.
    if (error.status === 400) {
      state.period = 'custom';
      renderPeriods();
      $('#custom-error').textContent = Object.values(error.fieldErrors).flat()[0] ?? error.message;
      content.replaceChildren();
      return;
    }
    pageError(content, error, load);
  }
}

customForm.addEventListener('submit', (event) => {
  event.preventDefault();
  $('#custom-error').textContent = '';
  const from = customForm.from.value;
  const to = customForm.to.value;
  if (!from || !to) {
    $('#custom-error').textContent = 'Isi kedua tanggal.';
    return;
  }
  if (from > to) {
    $('#custom-error').textContent = 'Tanggal "Dari" tidak boleh setelah tanggal "Sampai".';
    return;
  }
  Object.assign(state, { period: 'custom', from, to });
  load();
});

await startPage('report');
load();
