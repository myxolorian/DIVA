// Order wizard: 1) choose a saved customer (or add one), 2) choose services and quantities,
// 3) review, due date, payment, save. Totals shown here are a preview; the server calculates
// the real price when the order is saved.
import { api, ApiError } from '../api.js';
import { startPage } from '../layout.js';
import { openCustomerForm } from '../customer-form.js';
import { el, fill, icon, $, debounce, emptyState, skeletonRows, pageError, toast } from '../ui.js';
import { rupiah, number, unitLabel, priceText, initials, todayWib, addDays, date, quantity } from '../format.js';

const STEPS = ['Customer', 'Jasa', 'Konfirmasi'];
const state = {
  step: 0,
  customer: null,
  services: [],
  lines: new Map(), // serviceId -> { qty, unitPrice }
  dueDate: '',
  notes: '',
  payment: 'BelumLunas',
  saving: false,
  saved: false,
};

const sections = [$('#step-customer'), $('#step-services'), $('#step-review')];
const actionBar = $('#action-bar');
const actionButton = $('#action-button');

// ---------- calculations (mirror of the server rules, for the preview only) ----------

const round2 = (n) => Math.round(n * 100) / 100;
const serviceById = (id) => state.services.find((s) => s.id === id);

function lineTotal(service, line) {
  const billed = Math.max(line.qty, service.minQty ?? 0);
  const price = service.maxPrice ? (line.unitPrice ?? 0) : service.price;
  return { billed, price, subtotal: round2(billed * price) };
}

function pickedLines() {
  return [...state.lines].filter(([, line]) => line.qty > 0).map(([id, line]) => ({ service: serviceById(id), line }));
}

const total = () => pickedLines().reduce((sum, { service, line }) => sum + lineTotal(service, line).subtotal, 0);

function rangeProblem(service, line) {
  if (!service.maxPrice) return null;
  const p = line.unitPrice;
  if (p === undefined || p === null || Number.isNaN(p)) return 'Isi harga';
  if (p < service.price || p > service.maxPrice) return `Harga ${rupiah(service.price)} – ${rupiah(service.maxPrice)}`;
  return null;
}

// ---------- steps ----------

function renderSteps() {
  $('#steps').replaceChildren(...STEPS.map((name, i) =>
    el('div', { class: `step${i < state.step ? ' done' : ''}${i === state.step ? ' current' : ''}` },
      el('div', { class: 'bar' }), el('div', { class: 'name' }, `${i + 1}. ${name}`))));
}

function go(step) {
  state.step = step;
  sections.forEach((section, i) => section.classList.toggle('d-none', i !== step));
  renderSteps();
  if (step === 1) renderServices();
  if (step === 2) renderReview();
  updateActionBar();
  window.scrollTo({ top: 0, behavior: 'instant' });
}

function updateActionBar() {
  actionBar.classList.toggle('d-none', state.step === 0);
  const count = pickedLines().length;
  $('#action-total').textContent = rupiah(total());
  if (state.step === 1) {
    $('#action-label').textContent = count ? `${count} jasa dipilih` : 'Pilih minimal 1 jasa';
    actionButton.textContent = 'Lanjut';
    actionButton.disabled = count === 0;
  } else if (state.step === 2) {
    $('#action-label').textContent = 'Total';
    actionButton.textContent = 'Simpan order';
    actionButton.disabled = state.saving;
  }
}

// ---------- step 1: customer ----------

function customerRow(customer) {
  return el('button', { type: 'button', class: `list-row${state.customer?.id === customer.id ? ' selected' : ''}`, onclick: () => chooseCustomer(customer) },
    el('span', { class: 'avatar' }, initials(customer.name)),
    el('span', { class: 'grow' },
      el('span', { class: 'title d-block' }, customer.name),
      el('span', { class: 'sub d-block' }, [customer.phone, customer.address].filter(Boolean).join(' · '))),
    icon('bi-chevron-right', 'muted'));
}

let customerRequest = 0;
async function searchCustomers(term) {
  const results = $('#customer-results');
  const id = ++customerRequest;
  results.replaceChildren(skeletonRows(5));
  try {
    const page = await api('/api/customers', { query: { search: term, pageSize: 30 } });
    if (id !== customerRequest) return;
    results.replaceChildren(page.items.length
      ? el('div', { class: 'list-card' }, page.items.map(customerRow))
      : el('div', { class: 'card-soft' }, emptyState('bi-person-plus', term ? `"${term}" belum tersimpan` : 'Belum ada customer',
          'Tambahkan sebagai customer baru.', el('button', { class: 'btn btn-primary', onclick: () => addCustomer(term) }, icon('bi-person-plus', 'me-1'), 'Customer baru'))));
  } catch (error) {
    pageError(results, error, () => searchCustomers(term));
  }
}

async function addCustomer(prefill = '') {
  const guess = /^[\d+\s-]+$/.test(prefill) ? { name: '', phone: prefill } : { name: prefill, phone: '' };
  const saved = await openCustomerForm(prefill ? guess : null);
  if (saved) chooseCustomer(saved);
}

function chooseCustomer(customer) {
  state.customer = customer;
  go(1);
}

function renderCustomerStep() {
  const input = el('input', { class: 'form-control', type: 'search', placeholder: 'Cari nama atau no. telp', 'aria-label': 'Cari customer', autocomplete: 'off' });
  input.addEventListener('input', debounce(() => searchCustomers(input.value.trim()), 250));
  sections[0].replaceChildren(
    el('h2', { class: 'h6 fw-bold mb-3' }, 'Pilih customer'),
    el('div', { class: 'd-flex gap-2 mb-3' },
      el('div', { class: 'search flex-grow-1' }, icon('bi-search'), input),
      el('button', { type: 'button', class: 'btn btn-primary d-flex align-items-center gap-1', onclick: () => addCustomer(input.value.trim()) },
        icon('bi-person-plus'), el('span', { class: 'd-none d-sm-inline' }, 'Baru'))),
    el('div', { id: 'customer-results' }));
  searchCustomers('');
  setTimeout(() => input.focus(), 50);
}

// ---------- step 2: services ----------

function parseQty(value) {
  const n = Number(String(value).replace(',', '.'));
  return Number.isFinite(n) && n > 0 ? round2(n) : 0;
}

function setQty(service, qty) {
  const line = state.lines.get(service.id) ?? {};
  const clean = service.unit === 'Pcs' ? Math.max(0, Math.round(qty)) : Math.max(0, round2(qty));
  if (clean <= 0) state.lines.delete(service.id);
  else state.lines.set(service.id, { ...line, qty: clean });
  updateServiceRow(service);
  updateActionBar();
}

const stepSize = (service) => (service.unit === 'Pcs' ? 1 : 0.5);

function serviceControls(service) {
  const line = state.lines.get(service.id);
  if (!line) {
    return el('button', { type: 'button', class: 'btn btn-outline-primary btn-sm px-3', onclick: () => setQty(service, service.unit === 'Pcs' ? 1 : (service.minQty ?? 1)), 'aria-label': `Tambah ${service.name}` },
      icon('bi-plus-lg', 'me-1'), 'Tambah');
  }
  const input = el('input', { class: 'form-control form-control-sm num', type: 'number', inputmode: service.unit === 'Pcs' ? 'numeric' : 'decimal',
    min: 0, step: stepSize(service), value: line.qty, 'aria-label': `Qty ${service.name} (${unitLabel(service.unit)})` });
  input.addEventListener('change', () => setQty(service, parseQty(input.value)));
  return el('div', { class: 'qty-stepper' },
    el('button', { type: 'button', class: 'btn btn-light', 'aria-label': 'Kurangi', onclick: () => setQty(service, line.qty - stepSize(service)) }, icon(line.qty - stepSize(service) <= 0 ? 'bi-trash3' : 'bi-dash-lg')),
    input,
    el('button', { type: 'button', class: 'btn btn-primary', 'aria-label': 'Tambah', onclick: () => setQty(service, line.qty + stepSize(service)) }, icon('bi-plus-lg')));
}

function serviceExtra(service) {
  const line = state.lines.get(service.id);
  if (!line) return null;
  const parts = [];
  const { billed, subtotal } = lineTotal(service, line);
  if (service.maxPrice) {
    const price = el('input', { class: 'form-control form-control-sm num', type: 'number', inputmode: 'numeric', min: service.price, max: service.maxPrice, step: 1000,
      value: line.unitPrice ?? '', placeholder: `${number(service.price)} – ${number(service.maxPrice)}`, 'aria-label': `Harga ${service.name}` });
    price.addEventListener('input', () => {
      line.unitPrice = price.value === '' ? undefined : Number(price.value);
      price.classList.toggle('is-invalid', Boolean(rangeProblem(service, line)));
      updateSubtotal(service);
      updateActionBar();
    });
    if (line.unitPrice !== undefined && rangeProblem(service, line)) price.classList.add('is-invalid');
    parts.push(el('div', { class: 'd-flex align-items-center gap-2 mt-2' }, el('span', { class: 'small fw-semibold text-nowrap' }, 'Harga / pcs'), price));
  }
  if (billed > line.qty) {
    parts.push(el('div', { class: 'small text-warning-emphasis mt-1' }, icon('bi-info-circle', 'me-1'), `Kurang dari minimum: ditagih ${number(billed)} ${unitLabel(service.unit)}`));
  }
  parts.push(el('div', { class: 'small fw-semibold mt-1 num', dataset: { subtotal: service.id } }, `Subtotal ${rupiah(subtotal)}`));
  return parts;
}

function updateSubtotal(service) {
  const node = document.querySelector(`[data-subtotal="${service.id}"]`);
  const line = state.lines.get(service.id);
  if (node && line) node.textContent = `Subtotal ${rupiah(lineTotal(service, line).subtotal)}`;
}

function serviceRow(service) {
  const picked = state.lines.has(service.id);
  return el('div', { class: `service-row${picked ? ' picked' : ''}`, id: `svc-${service.id}` },
    el('div', { class: 'grow' },
      el('div', { class: 'name' }, service.name),
      el('div', { class: 'price num' }, priceText(service)),
      serviceExtra(service)),
    serviceControls(service));
}

function updateServiceRow(service) {
  document.getElementById(`svc-${service.id}`)?.replaceWith(serviceRow(service));
}

function renderServices() {
  const groups = new Map();
  for (const s of state.services) {
    if (!groups.has(s.category)) groups.set(s.category, []);
    groups.get(s.category).push(s);
  }
  const slug = (text) => `cat-${text.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
  sections[1].replaceChildren(
    el('div', { class: 'card-soft p-3 mb-3 d-flex align-items-center gap-3' },
      el('span', { class: 'avatar' }, initials(state.customer.name)),
      el('div', { class: 'flex-grow-1 min-w-0' }, el('div', { class: 'fw-bold text-truncate' }, state.customer.name), el('div', { class: 'small muted' }, state.customer.phone)),
      el('button', { type: 'button', class: 'btn btn-sm btn-light', onclick: () => go(0) }, 'Ganti')),
    el('div', { class: 'category-nav' }, el('div', { class: 'chips' }, [...groups.keys()].map((name) =>
      el('button', { type: 'button', class: 'chip', onclick: () => document.getElementById(slug(name))?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }, name)))),
    ...[...groups].flatMap(([name, services]) => [
      el('h2', { class: 'section-title', id: slug(name), style: 'scroll-margin-top:4rem' }, name),
      el('div', { class: 'list-card' }, services.map(serviceRow)),
    ]));
}

// ---------- step 3: review ----------

function dueChip(label, value) {
  return el('button', { type: 'button', class: `chip${state.dueDate === value ? ' active' : ''}`, onclick: () => { state.dueDate = value; renderReview(); } }, label);
}

function renderReview() {
  const today = todayWib();
  const dateInput = el('input', { class: 'form-control', type: 'date', min: today, value: state.dueDate, 'aria-label': 'Tanggal selesai' });
  dateInput.addEventListener('change', () => { state.dueDate = dateInput.value; renderReview(); });
  const notes = el('textarea', { class: 'form-control', rows: 2, maxlength: 500, placeholder: 'Contoh: pisahkan baju putih', 'aria-label': 'Catatan' });
  notes.value = state.notes;
  notes.addEventListener('input', () => { state.notes = notes.value; });

  const paymentOption = (value, label, iconName) => el('button', { type: 'button', class: `btn flex-fill ${state.payment === value ? 'btn-primary' : 'btn-light'}`, 'aria-pressed': String(state.payment === value),
    onclick: () => { state.payment = value; renderReview(); } }, icon(iconName, 'me-1'), label);

  fill(sections[2],
    el('div', { class: 'card-soft p-3 mb-3 d-flex align-items-center gap-3' },
      el('span', { class: 'avatar' }, initials(state.customer.name)),
      el('div', { class: 'flex-grow-1 min-w-0' }, el('div', { class: 'fw-bold text-truncate' }, state.customer.name), el('div', { class: 'small muted' }, state.customer.phone)),
      el('button', { type: 'button', class: 'btn btn-sm btn-light', onclick: () => go(0) }, 'Ganti')),

    el('div', { class: 'd-flex align-items-center' }, el('h2', { class: 'section-title flex-grow-1' }, 'Rincian'),
      el('button', { type: 'button', class: 'btn btn-sm btn-link mt-3', onclick: () => go(1) }, 'Ubah')),
    el('div', { class: 'list-card mb-3' },
      pickedLines().map(({ service, line }) => {
        const { billed, price, subtotal } = lineTotal(service, line);
        return el('div', { class: 'list-row' },
          el('span', { class: 'grow' },
            el('span', { class: 'title d-block', style: 'white-space:normal' }, service.name),
            el('span', { class: 'sub d-block', style: 'white-space:normal' }, `${quantity(line.qty, billed, service.unit)} × ${rupiah(price)}`)),
          el('span', { class: 'fw-semibold num' }, rupiah(subtotal)));
      }),
      el('div', { class: 'list-row' }, el('span', { class: 'grow fw-bold' }, 'Total'), el('span', { class: 'fw-bold fs-5 num' }, rupiah(total())))),

    el('h2', { class: 'section-title' }, 'Tanggal selesai'),
    el('div', { class: 'chips mb-2' },
      dueChip('Belum ditentukan', ''), dueChip('Besok', addDays(today, 1)), dueChip('2 hari', addDays(today, 2)), dueChip('3 hari', addDays(today, 3))),
    el('div', { class: 'd-flex align-items-center gap-2 mb-1' }, el('span', { class: 'small muted text-nowrap' }, 'atau pilih tanggal'), dateInput),
    state.dueDate ? el('div', { class: 'small text-primary fw-semibold' }, icon('bi-calendar-check', 'me-1'), `Selesai ${date(state.dueDate)}`) : null,

    el('h2', { class: 'section-title' }, 'Pembayaran'),
    el('div', { class: 'd-flex gap-2' },
      paymentOption('BelumLunas', 'Belum lunas', 'bi-hourglass-split'),
      paymentOption('Lunas', 'Sudah lunas', 'bi-cash-coin')),

    el('h2', { class: 'section-title' }, 'Catatan'),
    notes);
}

// ---------- save ----------

async function save() {
  const problem = pickedLines().find(({ service, line }) => rangeProblem(service, line));
  if (problem) {
    toast(`${problem.service.name}: ${rangeProblem(problem.service, problem.line)}.`, 'danger');
    go(1);
    document.getElementById(`svc-${problem.service.id}`)?.scrollIntoView({ block: 'center' });
    return;
  }
  const picked = pickedLines();
  state.saving = true;
  updateActionBar();
  actionButton.replaceChildren(el('span', { class: 'spinner-border spinner-border-sm me-2' }), 'Menyimpan…');
  try {
    const order = await api('/api/orders', {
      method: 'POST',
      body: {
        customerId: state.customer.id,
        items: picked.map(({ service, line }) => ({ serviceId: service.id, qty: line.qty, unitPrice: service.maxPrice ? line.unitPrice : undefined })),
        notes: state.notes || undefined,
        dueDate: state.dueDate || undefined,
        paymentStatus: state.payment,
      },
    });
    state.saved = true;
    location.replace(`/order.html?id=${order.id}&new=1`);
  } catch (error) {
    state.saving = false;
    updateActionBar();
    if (error instanceof ApiError && error.status === 400) {
      // Translate "items[1].qty" back to the service name so the message is clear.
      const messages = Object.entries(error.fieldErrors).map(([field, list]) => {
        const match = field.match(/^items\[(\d+)\]/);
        const name = match ? picked[Number(match[1])]?.service.name : null;
        return `${name ? `${name}: ` : ''}${list.join(' ')}`;
      });
      toast(messages.join(' ') || error.message, 'danger');
    } else {
      toast(error.message, 'danger');
    }
  }
}

actionButton.addEventListener('click', () => {
  if (state.step === 1) go(2);
  else if (state.step === 2) save();
});

$('#back').addEventListener('click', () => {
  if (state.step > 0) go(state.step - 1);
  else if (history.length > 1) history.back();
  else location.href = '/';
});

window.addEventListener('beforeunload', (event) => {
  if (state.lines.size > 0 && !state.saved) event.preventDefault();
});

// ---------- start ----------

await startPage('orders');
renderSteps();
try {
  state.services = await api('/api/services');
} catch (error) {
  pageError(sections[0], error, () => location.reload());
  throw error;
}

const preselected = new URLSearchParams(location.search).get('customerId');
renderCustomerStep();
if (preselected) {
  try {
    chooseCustomer(await api(`/api/customers/${preselected}`));
  } catch {
    toast('Customer tidak ditemukan, silakan pilih ulang.', 'danger');
  }
}
