import { api } from '../api.js';
import { startPage } from '../layout.js';
import { dailyChart } from '../chart.js';
import { el, icon, $, emptyState, pageError } from '../ui.js';
import { rupiah, number, longToday, dateTime, dueText, statuses, statusOrder, payments } from '../format.js';

const content = $('#content');

function stat(label, value, hint, extra = '') {
  return el('div', { class: `stat ${extra}` }, el('div', { class: 'label' }, label), el('div', { class: 'value num' }, value), hint ? el('div', { class: 'hint' }, hint) : null);
}

function orderRow(order) {
  const status = statuses[order.status];
  const open = order.status === 'Baru' || order.status === 'Diproses';
  const due = open ? dueText(order.dueDate) : null;
  return el('a', { class: 'list-row', href: `/order.html?id=${order.id}` },
    el('span', { class: 'avatar' }, icon(status.icon)),
    el('span', { class: 'grow' },
      el('span', { class: 'title d-block' }, order.customerName),
      el('span', { class: 'sub d-block' }, `${order.orderNumber} · ${dateTime(order.createdAt)}`),
      due ? el('span', { class: `small fw-semibold ${due.startsWith('Terlambat') ? 'text-danger' : 'text-primary'}` }, icon('bi-clock', 'me-1'), `Selesai: ${due}`) : null),
    el('span', { class: 'text-end' },
      el('span', { class: 'fw-bold num d-block' }, rupiah(order.total)),
      order.paymentStatus === 'Lunas' ? null : el('span', { class: 'badge text-bg-danger-subtle text-danger-emphasis' }, payments.BelumLunas.label)));
}

function render(s) {
  const nothingYet = s.recentOrders.length === 0;
  const alerts = [];
  if (s.overdue > 0) {
    alerts.push(el('a', { class: 'alert alert-danger d-flex align-items-center gap-2 text-decoration-none mb-2', href: '/orders.html?status=Diproses' },
      icon('bi-exclamation-octagon'), el('span', { class: 'grow' }, el('strong', {}, `${s.overdue} order terlambat`), ' — tanggal selesainya sudah lewat.')));
  }
  if (s.dueToday > 0) {
    alerts.push(el('a', { class: 'alert alert-warning d-flex align-items-center gap-2 text-decoration-none mb-2', href: '/orders.html?status=Diproses' },
      icon('bi-alarm'), el('span', {}, el('strong', {}, `${s.dueToday} order`), ' harus selesai hari ini.')));
  }

  content.replaceChildren(
    ...alerts,
    el('div', { class: 'row g-3' },
      // Money that came in today (orders marked paid today); the card opens the income report.
      el('div', { class: 'col-12 col-md-6' }, el('a', { class: 'status-tile', href: '/laporan.html' },
        stat('Pemasukan hari ini', rupiah(s.income.today),
          el('span', { class: 'd-flex justify-content-between gap-2' },
            el('span', {}, `Bulan ini ${rupiah(s.income.thisMonth)}`),
            el('span', { class: 'text-nowrap' }, 'Laporan', icon('bi-chevron-right', 'ms-1'))), 'stat-hero'))),
      el('div', { class: 'col-6 col-md-3' }, el('a', { class: 'status-tile', href: '/orders.html' },
        stat('Order masuk hari ini', rupiah(s.today.total), `${number(s.today.orders)} order`))),
      el('div', { class: 'col-6 col-md-3' }, el('a', { class: 'status-tile', href: '/orders.html?payment=BelumLunas' },
        stat('Belum lunas', rupiah(s.unpaid.total), `${number(s.unpaid.orders)} order`)))),

    el('h2', { class: 'section-title' }, 'Status order'),
    el('div', { class: 'row g-2' }, statusOrder.map((key) => el('div', { class: 'col-6 col-md-3' },
      el('a', { class: 'status-tile', href: `/orders.html?status=${key}` },
        el('div', { class: 'stat d-flex align-items-center gap-3' },
          el('span', { class: 'avatar' }, icon(statuses[key].icon)),
          el('div', {}, el('div', { class: 'value num', style: 'font-size:1.25rem' }, number(s.statusCounts[key.charAt(0).toLowerCase() + key.slice(1)])),
            el('div', { class: 'label' }, statuses[key].label))))))),

    el('h2', { class: 'section-title' }, 'Nilai order 7 hari terakhir'),
    el('div', { class: 'card-soft p-3' }, dailyChart(s.last7Days)),

    el('div', { class: 'd-flex align-items-center' },
      el('h2', { class: 'section-title flex-grow-1' }, 'Order terbaru'),
      nothingYet ? null : el('a', { class: 'small fw-semibold mt-3', href: '/orders.html' }, 'Lihat semua')),
    nothingYet
      ? el('div', { class: 'card-soft' }, emptyState('bi-basket', 'Belum ada order', 'Order pertama Anda akan muncul di sini.',
          el('a', { class: 'btn btn-primary', href: '/order-new.html' }, icon('bi-plus-lg', 'me-1'), 'Buat order pertama')))
      : el('div', { class: 'list-card' }, s.recentOrders.map(orderRow)));
}

function greeting() {
  const hour = Number(new Intl.DateTimeFormat('en-GB', { hour: 'numeric', hour12: false, timeZone: 'Asia/Jakarta' }).format(new Date()));
  if (hour < 11) return 'Selamat pagi';
  if (hour < 15) return 'Selamat siang';
  if (hour < 18) return 'Selamat sore';
  return 'Selamat malam';
}

async function load() {
  content.replaceChildren(el('div', { class: 'row g-3' }, [1, 2, 3].map(() => el('div', { class: 'col-12 col-md-4' }, el('div', { class: 'skeleton', style: 'height:6.5rem' })))));
  try {
    render(await api('/api/dashboard/summary'));
  } catch (error) {
    pageError(content, error, load);
  }
}

await startPage('dashboard');
$('#today').textContent = longToday();
$('#greeting').textContent = `${greeting()} 👋`;
load();

