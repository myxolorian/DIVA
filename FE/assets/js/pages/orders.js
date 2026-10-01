import { api } from '../api.js';
import { startPage } from '../layout.js';
import { el, icon, $, debounce, emptyState, skeletonRows, pageError, badge, toast } from '../ui.js';
import { rupiah, dateTime, dueText, statuses, statusOrder, payments } from '../format.js';

const params = new URLSearchParams(location.search);
const state = {
  status: params.get('status') ?? '',
  payment: params.get('payment') ?? '',
  search: params.get('search') ?? '',
  customerId: params.get('customerId') ?? '',
  page: 1,
  items: [],
  total: 0,
};

const list = $('#list');
const more = $('#more');
const search = $('#search');
search.value = state.search;

function syncUrl() {
  const q = new URLSearchParams();
  for (const key of ['status', 'payment', 'search', 'customerId']) if (state[key]) q.set(key, state[key]);
  history.replaceState(null, '', q.toString() ? `?${q}` : location.pathname);
}

function chip(label, active, onclick, count) {
  return el('button', { type: 'button', class: `chip${active ? ' active' : ''}`, 'aria-pressed': String(active), onclick },
    label, count === undefined ? null : el('span', { class: 'count' }, count));
}

function renderChips(counts) {
  $('#status-chips').replaceChildren(
    chip('Semua', !state.status, () => setFilter('status', '')),
    ...statusOrder.map((key) => chip(statuses[key].label, state.status === key, () => setFilter('status', key),
      counts ? counts[key.charAt(0).toLowerCase() + key.slice(1)] : undefined)));
  $('#payment-chips').replaceChildren(
    chip('Semua pembayaran', !state.payment, () => setFilter('payment', '')),
    chip('Belum lunas', state.payment === 'BelumLunas', () => setFilter('payment', 'BelumLunas')),
    chip('Lunas', state.payment === 'Lunas', () => setFilter('payment', 'Lunas')));
}

let counts;
function setFilter(key, value) {
  state[key] = value;
  renderChips(counts);
  reload();
}

function row(order) {
  const status = statuses[order.status];
  const open = order.status === 'Baru' || order.status === 'Diproses';
  const due = open ? dueText(order.dueDate) : null;
  return el('a', { class: 'list-row', href: `/order.html?id=${order.id}` },
    el('span', { class: 'avatar' }, icon(status.icon)),
    el('span', { class: 'grow' },
      el('span', { class: 'title d-block' }, order.customerName),
      el('span', { class: 'sub d-block' }, `${order.orderNumber} · ${dateTime(order.createdAt)}`),
      el('span', { class: 'd-flex flex-wrap gap-1 mt-1' },
        badge(status),
        badge(payments[order.paymentStatus]),
        due ? el('span', { class: `badge badge-status ${due.startsWith('Terlambat') ? 'text-bg-danger' : 'bg-primary-soft text-primary'}` }, icon('bi-clock', 'me-1'), due) : null)),
    el('span', { class: 'fw-bold num text-end' }, rupiah(order.total)));
}

function render() {
  if (state.items.length === 0) {
    const filtered = state.status || state.payment || state.search || state.customerId;
    list.replaceChildren(el('div', { class: 'card-soft' }, filtered
      ? emptyState('bi-search', 'Tidak ada order yang cocok', 'Coba ubah filter atau kata kunci pencarian.')
      : emptyState('bi-basket', 'Belum ada order', null, el('a', { class: 'btn btn-primary', href: '/order-new.html' }, icon('bi-plus-lg', 'me-1'), 'Buat order'))));
  } else {
    list.replaceChildren(
      el('div', { class: 'small muted mb-2' }, `${state.total} order`),
      el('div', { class: 'list-card' }, state.items.map(row)));
  }
  more.classList.toggle('d-none', state.items.length >= state.total);
}

async function fetchPage() {
  return api('/api/orders', {
    query: { status: state.status, paymentStatus: state.payment, search: state.search, customerId: state.customerId, page: state.page, pageSize: 20 },
  });
}

let requestId = 0;
async function reload() {
  syncUrl();
  state.page = 1;
  const id = ++requestId;
  list.replaceChildren(skeletonRows(5));
  more.classList.add('d-none');
  try {
    const result = await fetchPage();
    if (id !== requestId) return; // a newer search already started
    state.items = result.items;
    state.total = result.total;
    render();
  } catch (error) {
    pageError(list, error, reload);
  }
}

more.addEventListener('click', async () => {
  more.disabled = true;
  state.page += 1;
  try {
    const result = await fetchPage();
    state.items.push(...result.items);
    render();
  } finally {
    more.disabled = false;
  }
});

search.addEventListener('input', debounce(() => {
  state.search = search.value.trim();
  reload();
}, 300));

await startPage('orders');
// Coming back from a deleted order (order.html). syncUrl() drops the parameter from the address.
if (params.get('deleted')) toast(`Order ${params.get('deleted')} dihapus.`);

if (state.customerId) {
  api(`/api/customers/${state.customerId}`).then((customer) => {
    $('#customer-filter').replaceChildren(el('div', { class: 'alert alert-light border d-flex align-items-center gap-2 py-2' },
      icon('bi-person'), el('span', { class: 'grow' }, 'Order milik ', el('strong', {}, customer.name)),
      el('button', { type: 'button', class: 'btn btn-sm btn-link', onclick: () => { state.customerId = ''; $('#customer-filter').replaceChildren(); reload(); } }, 'Hapus filter')));
  }).catch(() => {});
}

renderChips();
api('/api/dashboard/summary').then((summary) => { counts = summary.statusCounts; renderChips(counts); }).catch(() => {});
reload();
