import { api } from '../api.js';
import { startPage } from '../layout.js';
import { openCustomerForm } from '../customer-form.js';
import { el, icon, $, debounce, emptyState, skeletonRows, pageError, toast, confirmDialog } from '../ui.js';
import { initials, waLink } from '../format.js';

const state = { search: '', page: 1, items: [], total: 0 };
const list = $('#list');
const more = $('#more');

// Two buttons side by side (a button may not contain another button):
// the name opens the detail sheet, the bin deletes straight from the list.
function row(customer) {
  return el('div', { class: 'list-row split' },
    el('button', { type: 'button', class: 'main', onclick: () => openDetail(customer) },
      el('span', { class: 'avatar' }, initials(customer.name)),
      el('span', { class: 'grow' },
        el('span', { class: 'title d-block' }, customer.name),
        el('span', { class: 'sub d-block' }, [customer.phone, customer.address].filter(Boolean).join(' · ')))),
    el('button', { type: 'button', class: 'row-action', title: 'Hapus customer', 'aria-label': `Hapus ${customer.name}`, onclick: () => deleteCustomer(customer) },
      icon('bi-trash')));
}

async function deleteCustomer(customer) {
  const ok = await confirmDialog(`${customer.name} akan dihapus dari daftar customer. Order lama tetap tersimpan.`,
    { title: 'Hapus customer?', okText: 'Hapus', danger: true });
  if (!ok) return;
  try {
    await api(`/api/customers/${customer.id}`, { method: 'DELETE' });
    toast(`${customer.name} dihapus.`);
    reload();
  } catch (error) {
    toast(error.message, 'danger');
  }
}

function render() {
  if (state.items.length === 0) {
    list.replaceChildren(el('div', { class: 'card-soft' }, state.search
      ? emptyState('bi-search', 'Customer tidak ditemukan', `Tidak ada nama atau no. telp yang cocok dengan "${state.search}".`,
          el('button', { class: 'btn btn-primary', onclick: addCustomer }, icon('bi-person-plus', 'me-1'), 'Tambah customer'))
      : emptyState('bi-people', 'Belum ada customer', 'Customer tersimpan akan muncul di sini dan bisa dipilih saat membuat order.',
          el('button', { class: 'btn btn-primary', onclick: addCustomer }, icon('bi-person-plus', 'me-1'), 'Tambah customer'))));
  } else {
    list.replaceChildren(el('div', { class: 'small muted mb-2' }, `${state.total} customer`), el('div', { class: 'list-card' }, state.items.map(row)));
  }
  more.classList.toggle('d-none', state.items.length >= state.total);
}

const fetchPage = () => api('/api/customers', { query: { search: state.search, page: state.page, pageSize: 30 } });

let requestId = 0;
async function reload() {
  state.page = 1;
  const id = ++requestId;
  list.replaceChildren(skeletonRows(6));
  try {
    const result = await fetchPage();
    if (id !== requestId) return;
    state.items = result.items;
    state.total = result.total;
    render();
  } catch (error) {
    pageError(list, error, reload);
  }
}

async function addCustomer() {
  const saved = await openCustomerForm();
  if (saved) {
    toast(`${saved.name} tersimpan.`);
    reload();
  }
}

function openDetail(customer) {
  const sheet = el('div', { class: 'offcanvas offcanvas-bottom', tabindex: '-1', 'aria-labelledby': 'cd-title' });
  // Bootstrap needs the element to be in the page before the offcanvas is created.
  document.body.append(sheet);
  const offcanvas = new bootstrap.Offcanvas(sheet);
  let next = null;

  const action = (iconName, label, onclick, cls = 'btn-light') =>
    el('button', { type: 'button', class: `btn ${cls} d-flex flex-column align-items-center gap-1 py-2 flex-fill`, onclick }, icon(iconName, 'fs-5'), el('span', { class: 'small' }, label));

  sheet.append(
    el('div', { class: 'offcanvas-header' },
      el('div', { class: 'd-flex align-items-center gap-3' },
        el('span', { class: 'avatar', style: 'width:3.2rem;height:3.2rem;font-size:1.1rem' }, initials(customer.name)),
        el('div', {}, el('h2', { class: 'offcanvas-title h5 fw-bold mb-0', id: 'cd-title' }, customer.name), el('div', { class: 'muted small' }, customer.phone))),
      el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'offcanvas', 'aria-label': 'Tutup' })),
    el('div', { class: 'offcanvas-body pt-0' },
      el('a', { class: 'btn btn-primary btn-lg w-100 mb-3', href: `/order-new.html?customerId=${customer.id}` }, icon('bi-plus-lg', 'me-1'), 'Buat order untuk customer ini'),
      el('div', { class: 'd-flex gap-2 mb-3' },
        el('a', { class: 'btn btn-light d-flex flex-column align-items-center gap-1 py-2 flex-fill', href: waLink(customer.phone, `Halo ${customer.name}, `), target: '_blank', rel: 'noopener' }, icon('bi-whatsapp', 'fs-5 text-success'), el('span', { class: 'small' }, 'WhatsApp')),
        el('a', { class: 'btn btn-light d-flex flex-column align-items-center gap-1 py-2 flex-fill', href: `tel:${customer.phone}` }, icon('bi-telephone', 'fs-5'), el('span', { class: 'small' }, 'Telepon')),
        el('a', { class: 'btn btn-light d-flex flex-column align-items-center gap-1 py-2 flex-fill', href: `/orders.html?customerId=${customer.id}` }, icon('bi-receipt', 'fs-5'), el('span', { class: 'small' }, 'Order'))),
      customer.address ? el('div', { class: 'mb-2' }, el('div', { class: 'small muted fw-semibold' }, 'Alamat'), customer.address) : null,
      customer.notes ? el('div', { class: 'mb-2' }, el('div', { class: 'small muted fw-semibold' }, 'Catatan'), customer.notes) : null,
      el('div', { class: 'd-flex gap-2 mt-3' },
        action('bi-pencil', 'Ubah', () => { next = 'edit'; offcanvas.hide(); }),
        action('bi-trash', 'Hapus', () => { next = 'delete'; offcanvas.hide(); }, 'btn-light text-danger'))));

  sheet.addEventListener('hidden.bs.offcanvas', async () => {
    sheet.remove();
    if (next === 'edit') {
      const saved = await openCustomerForm(customer);
      if (saved) { toast('Perubahan tersimpan.'); reload(); }
    } else if (next === 'delete') {
      await deleteCustomer(customer);
    }
  });
  offcanvas.show();
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

$('#search').addEventListener('input', debounce((event) => { state.search = event.target.value.trim(); reload(); }, 300));
$('#add').addEventListener('click', addCustomer);

await startPage('customers');
reload();
