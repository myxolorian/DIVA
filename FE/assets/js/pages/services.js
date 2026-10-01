import { api, ApiError } from '../api.js';
import { startPage } from '../layout.js';
import { el, icon, $, emptyState, skeletonRows, pageError, toast, withBusy, showFieldErrors, clearFieldErrors } from '../ui.js';
import { units, priceText } from '../format.js';

const list = $('#list');
const showInactive = $('#show-inactive');
let services = [];

function groupByCategory(items) {
  const groups = new Map();
  for (const item of items) {
    if (!groups.has(item.category)) groups.set(item.category, []);
    groups.get(item.category).push(item);
  }
  return groups;
}

function render() {
  if (services.length === 0) {
    list.replaceChildren(el('div', { class: 'card-soft' }, emptyState('bi-tags', 'Belum ada jasa', null,
      el('button', { class: 'btn btn-primary', onclick: () => openForm() }, icon('bi-plus-lg', 'me-1'), 'Tambah jasa'))));
    return;
  }
  list.replaceChildren(...[...groupByCategory(services)].flatMap(([category, items]) => [
    el('h2', { class: 'section-title' }, category),
    el('div', { class: 'list-card' }, items.map((s) => el('button', { type: 'button', class: `list-row${s.isActive ? '' : ' opacity-50'}`, onclick: () => openForm(s) },
      el('span', { class: 'grow' },
        el('span', { class: 'title d-block' }, s.name, s.isActive ? null : el('span', { class: 'badge text-bg-secondary ms-2' }, 'Nonaktif')),
        el('span', { class: 'sub d-block num' }, priceText(s))),
      icon('bi-pencil', 'muted')))),
  ]));
}

async function load() {
  list.replaceChildren(skeletonRows(6));
  try {
    services = await api('/api/services', { query: { includeInactive: showInactive.checked } });
    render();
  } catch (error) {
    pageError(list, error, load);
  }
}

function input(label, name, attrs = {}, help) {
  const id = `sf-${name}`;
  return el('div', { class: 'mb-3' },
    el('label', { class: 'form-label', for: id }, label),
    el('input', { class: 'form-control', id, name, ...attrs }),
    help ? el('div', { class: 'form-text' }, help) : null);
}

function openForm(service = null) {
  const categories = [...new Set(services.map((s) => s.category))];
  const unitSelect = el('select', { class: 'form-select', id: 'sf-unit', name: 'unit' },
    Object.entries(units).map(([value, label]) => el('option', { value, selected: service?.unit === value }, label)));

  const form = el('form', { novalidate: true },
    input('Nama jasa', 'name', { required: true, maxlength: 120, value: service?.name ?? '' }),
    input('Kategori', 'category', { required: true, maxlength: 60, list: 'sf-categories', value: service?.category ?? '', placeholder: 'Contoh: Laundry Kiloan' }),
    el('datalist', { id: 'sf-categories' }, categories.map((c) => el('option', { value: c }))),
    el('div', { class: 'row g-2' },
      el('div', { class: 'col-5' }, el('div', { class: 'mb-3' }, el('label', { class: 'form-label', for: 'sf-unit' }, 'Satuan'), unitSelect)),
      el('div', { class: 'col-7' }, input('Harga (Rp)', 'price', { required: true, type: 'number', min: 1, step: 'any', inputmode: 'decimal', value: service?.price ?? '' }))),
    el('div', { class: 'row g-2' },
      el('div', { class: 'col-6' }, input('Minimal qty', 'minQty', { type: 'number', min: 0, step: 'any', inputmode: 'decimal', value: service?.minQty ?? '' }, 'Kosongkan jika tidak ada.')),
      el('div', { class: 'col-6' }, input('Harga maksimal', 'maxPrice', { type: 'number', min: 0, step: 'any', inputmode: 'decimal', value: service?.maxPrice ?? '' }, 'Isi untuk harga range.'))),
    el('div', { class: 'form-check form-switch mb-3' },
      el('input', { class: 'form-check-input', type: 'checkbox', role: 'switch', id: 'sf-active', name: 'isActive', checked: service ? service.isActive : true }),
      el('label', { class: 'form-check-label', for: 'sf-active' }, 'Aktif (bisa dipilih saat membuat order)')));

  const save = el('button', { type: 'submit', class: 'btn btn-primary btn-lg w-100' }, service ? 'Simpan perubahan' : 'Tambah jasa');
  form.append(save);

  const sheet = el('div', { class: 'offcanvas offcanvas-bottom', tabindex: '-1', 'aria-labelledby': 'sf-title' },
    el('div', { class: 'offcanvas-header' },
      el('h2', { class: 'offcanvas-title h5 fw-bold', id: 'sf-title' }, service ? 'Ubah jasa' : 'Jasa baru'),
      el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'offcanvas', 'aria-label': 'Tutup' })),
    el('div', { class: 'offcanvas-body pt-0' },
      service ? el('div', { class: 'alert alert-info small py-2' }, icon('bi-info-circle', 'me-1'), 'Perubahan harga hanya berlaku untuk order baru. Order lama tetap memakai harga saat order dibuat.') : null,
      form));
  document.body.append(sheet);
  const offcanvas = new bootstrap.Offcanvas(sheet);

  const toNumber = (value) => (value === '' ? null : Number(String(value).replace(',', '.')));

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearFieldErrors(form);
    if (!form.checkValidity()) {
      form.classList.add('was-validated');
      return;
    }
    const body = {
      name: form.name.value,
      category: form.category.value,
      unit: form.unit.value,
      price: toNumber(form.price.value),
      minQty: toNumber(form.minQty.value),
      maxPrice: toNumber(form.maxPrice.value),
      isActive: form.isActive.checked,
      sortOrder: service?.sortOrder,
    };
    await withBusy(save, async () => {
      try {
        if (service) await api(`/api/services/${service.id}`, { method: 'PUT', body });
        else await api('/api/services', { method: 'POST', body });
        offcanvas.hide();
        toast(service ? 'Jasa diperbarui.' : 'Jasa ditambahkan.');
        load();
      } catch (error) {
        if (error instanceof ApiError && error.status === 400) showFieldErrors(form, error);
        else toast(error.message, 'danger');
      }
    });
  });

  sheet.addEventListener('shown.bs.offcanvas', () => { if (!service) form.name.focus(); });
  sheet.addEventListener('hidden.bs.offcanvas', () => sheet.remove());
  offcanvas.show();
}

showInactive.addEventListener('change', load);
$('#add').addEventListener('click', () => openForm());

await startPage('services');
load();
