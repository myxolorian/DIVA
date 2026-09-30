import { api } from '../api.js';
import { startPage } from '../layout.js';
import { el, icon, $, badge, toast, pageError, confirmDialog } from '../ui.js';
import { rupiah, dateTime, date, dueText, quantity, statuses, statusOrder, payments, waLink, initials } from '../format.js';

const params = new URLSearchParams(location.search);
const id = params.get('id');
const justCreated = params.get('new') === '1';
const content = $('#content');
let order;
let outletName = 'DIVA Laundry';

const receiptUrl = () => new URL(order.receiptPath, location.origin).href;
const pdfUrl = () => `/api/public/receipts/${order.publicToken}/pdf`;

function whatsappText() {
  return `Halo ${order.customer.name}, terima kasih sudah menggunakan ${outletName}.\n`
    + `No. order: ${order.orderNumber}\nTotal: ${rupiah(order.total)} (${payments[order.paymentStatus].label})\n\n`
    + `Receipt: ${receiptUrl()}`;
}

async function copyLink() {
  try {
    await navigator.clipboard.writeText(receiptUrl());
    toast('Link receipt disalin.');
  } catch {
    prompt('Salin link receipt:', receiptUrl());
  }
}

async function update(path, body, message) {
  try {
    order = await api(`/api/orders/${id}/${path}`, { method: 'PATCH', body });
    render();
    toast(message);
  } catch (error) {
    toast(error.message, 'danger');
  }
}

function shareButtons(primary) {
  return el('div', { class: 'd-grid gap-2' },
    el('a', { class: `btn ${primary ? 'btn-success btn-lg' : 'btn-outline-success'}`, href: waLink(order.customer.phone, whatsappText()), target: '_blank', rel: 'noopener' },
      icon('bi-whatsapp', 'me-1'), `Kirim receipt ke WhatsApp ${order.customer.name.split(' ')[0]}`),
    el('div', { class: 'd-flex gap-2' },
      el('button', { type: 'button', class: 'btn btn-light flex-fill', onclick: copyLink }, icon('bi-link-45deg', 'me-1'), 'Salin link'),
      el('a', { class: 'btn btn-light flex-fill', href: order.receiptPath, target: '_blank', rel: 'noopener' }, icon('bi-box-arrow-up-right', 'me-1'), 'Lihat'),
      el('a', { class: 'btn btn-light flex-fill', href: pdfUrl() }, icon('bi-file-earmark-pdf', 'me-1'), 'PDF')));
}

function statusCard() {
  const index = statusOrder.indexOf(order.status);
  const next = statusOrder[index + 1];
  return el('div', { class: 'card-soft p-3 mb-3' },
    el('div', { class: 'progress-steps mb-3' }, statusOrder.map((key, i) =>
      el('div', { class: `ps${i < index ? ' done' : ''}${i === index ? ' current' : ''}` },
        el('div', { class: 'dot' }, icon(i < index ? 'bi-check-lg' : statuses[key].icon)),
        el('div', { class: 'name' }, statuses[key].label)))),
    el('div', { class: 'd-flex gap-2' },
      next
        ? el('button', { type: 'button', class: 'btn btn-primary flex-fill', onclick: () => update('status', { status: next }, `Status: ${statuses[next].label}`) },
            icon(statuses[next].icon, 'me-1'), `Tandai ${statuses[next].label.toLowerCase()}`)
        : el('div', { class: 'flex-fill small muted align-self-center' }, icon('bi-check2-all', 'me-1'), 'Order sudah selesai dan diambil.'),
      el('div', { class: 'dropdown' },
        el('button', { type: 'button', class: 'btn btn-light', 'data-bs-toggle': 'dropdown', 'aria-expanded': 'false', 'aria-label': 'Ubah status' }, icon('bi-three-dots')),
        el('ul', { class: 'dropdown-menu dropdown-menu-end' },
          el('li', {}, el('h6', { class: 'dropdown-header' }, 'Ubah status ke')),
          statusOrder.filter((key) => key !== order.status).map((key) =>
            el('li', {}, el('button', { type: 'button', class: 'dropdown-item', onclick: () => update('status', { status: key }, `Status: ${statuses[key].label}`) }, icon(statuses[key].icon, 'me-2'), statuses[key].label)))))));
}

function paymentCard() {
  const paid = order.paymentStatus === 'Lunas';
  return el('div', { class: 'card-soft p-3 mb-3 d-flex align-items-center gap-3' },
    el('div', { class: 'grow flex-grow-1' },
      el('div', { class: 'small muted fw-semibold' }, 'Pembayaran'),
      el('div', { class: 'd-flex align-items-center gap-2' }, el('span', { class: 'fs-4 fw-bold num' }, rupiah(order.total)), badge(payments[order.paymentStatus]))),
    paid
      ? el('button', { type: 'button', class: 'btn btn-light', onclick: async () => {
          if (await confirmDialog('Status pembayaran akan dikembalikan menjadi belum lunas.', { title: 'Batalkan lunas?', okText: 'Ya, belum lunas' })) {
            update('payment', { paymentStatus: 'BelumLunas' }, 'Ditandai belum lunas.');
          }
        } }, 'Batalkan')
      : el('button', { type: 'button', class: 'btn btn-success', onclick: () => update('payment', { paymentStatus: 'Lunas' }, 'Pembayaran lunas.') }, icon('bi-cash-coin', 'me-1'), 'Tandai lunas'));
}

function customerCard() {
  const c = order.customer;
  return el('div', { class: 'card-soft p-3 mb-3' },
    el('div', { class: 'd-flex align-items-center gap-3' },
      el('span', { class: 'avatar' }, initials(c.name)),
      el('div', { class: 'flex-grow-1 min-w-0' },
        el('div', { class: 'fw-bold text-truncate' }, c.name),
        el('div', { class: 'small muted' }, c.phone),
        c.address ? el('div', { class: 'small muted text-truncate' }, c.address) : null),
      el('a', { class: 'btn btn-light', href: waLink(c.phone, `Halo ${c.name}, `), target: '_blank', rel: 'noopener', 'aria-label': 'WhatsApp customer' }, icon('bi-whatsapp', 'text-success')),
      el('a', { class: 'btn btn-light', href: `tel:${c.phone}`, 'aria-label': 'Telepon customer' }, icon('bi-telephone'))));
}

function itemsCard() {
  return el('div', { class: 'list-card mb-3' },
    order.items.map((item) => el('div', { class: 'list-row' },
      el('span', { class: 'grow' },
        el('span', { class: 'title d-block', style: 'white-space:normal' }, item.serviceName),
        el('span', { class: 'sub d-block', style: 'white-space:normal' }, `${quantity(item.qty, item.billedQty, item.unit)} × ${rupiah(item.unitPrice)}`)),
      el('span', { class: 'fw-semibold num' }, rupiah(item.subtotal)))),
    el('div', { class: 'list-row' }, el('span', { class: 'grow fw-bold' }, 'Total'), el('span', { class: 'fw-bold fs-5 num' }, rupiah(order.total))));
}

function infoCard() {
  const due = order.dueDate ? `${date(order.dueDate)} (${dueText(order.dueDate)})` : 'Tidak ditentukan';
  const row = (label, value) => el('div', { class: 'd-flex justify-content-between gap-3 py-1' }, el('span', { class: 'muted' }, label), el('span', { class: 'text-end' }, value));
  return el('div', { class: 'card-soft p-3 mb-3 small' },
    row('No. order', order.orderNumber),
    row('Dibuat', dateTime(order.createdAt)),
    row('Tanggal selesai', due),
    order.notes ? row('Catatan', order.notes) : null);
}

function render() {
  $('#title').textContent = order.orderNumber;
  document.title = `${order.orderNumber} · DIVA Laundry`;
  content.replaceChildren(
    justCreated
      ? el('div', { class: 'card-soft p-3 mb-3 border-success' },
          el('div', { class: 'd-flex align-items-center gap-2 mb-3' }, icon('bi-check-circle-fill', 'text-success fs-3'),
            el('div', {}, el('div', { class: 'fw-bold' }, 'Order berhasil dibuat'), el('div', { class: 'small muted' }, 'Kirim receipt ke customer sekarang.'))),
          shareButtons(true))
      : null,
    statusCard(),
    paymentCard(),
    el('h2', { class: 'section-title' }, 'Customer'),
    customerCard(),
    el('h2', { class: 'section-title' }, 'Rincian'),
    itemsCard(),
    infoCard(),
    justCreated ? null : el('h2', { class: 'section-title' }, 'Receipt'),
    justCreated ? null : el('div', { class: 'card-soft p-3' }, shareButtons(false)));
}

async function load() {
  content.replaceChildren(el('div', { class: 'skeleton', style: 'height:10rem' }));
  try {
    order = await api(`/api/orders/${id}`);
    render();
  } catch (error) {
    pageError(content, error, load);
  }
}

await startPage('orders');
if (!id) location.replace('/orders.html');
api('/api/outlet').then((outlet) => { outletName = outlet.name; }).catch(() => {});
await load();
if (justCreated) history.replaceState(null, '', `?id=${id}`); // reload won't show the banner again
