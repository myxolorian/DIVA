// Bottom sheet with the customer form, used on the Customer page and in the order wizard.
import { api, ApiError } from './api.js';
import { el, icon, withBusy, showFieldErrors, clearFieldErrors, formData } from './ui.js';

function field(label, name, attrs = {}, textarea = false) {
  const id = `cf-${name}`;
  const input = textarea
    ? el('textarea', { class: 'form-control', id, name, rows: 2, ...attrs })
    : el('input', { class: 'form-control form-control-lg', id, name, ...attrs });
  return el('div', { class: 'mb-3' }, el('label', { class: 'form-label', for: id }, label), input);
}

/**
 * Opens the form. Resolves with the saved customer, or null when closed.
 * `customer` with an id = edit that customer; without an id = a new customer with prefilled fields.
 * When the phone number already belongs to someone, offers to use that customer instead.
 */
export function openCustomerForm(customer = null) {
  const editing = Boolean(customer?.id);
  return new Promise((resolve) => {
    let result = null;
    const form = el('form', { novalidate: true },
      field('Nama', 'name', { required: true, maxlength: 120, autocomplete: 'off', value: customer?.name ?? '' }),
      field('No. telepon / WhatsApp', 'phone', { required: true, inputmode: 'tel', maxlength: 30, placeholder: '0812…', value: customer?.phone ?? '' }),
      field('Alamat', 'address', { maxlength: 300 }, true),
      field('Catatan', 'notes', { maxlength: 500, placeholder: 'Contoh: tanpa pewangi' }, true));
    form.address.value = customer?.address ?? '';
    form.notes.value = customer?.notes ?? '';

    const conflict = el('div', { class: 'alert alert-warning small d-none', role: 'alert' });
    const save = el('button', { type: 'submit', class: 'btn btn-primary btn-lg w-100' }, editing ? 'Simpan perubahan' : 'Simpan customer');
    form.append(conflict, save);

    const sheet = el('div', { class: 'offcanvas offcanvas-bottom', tabindex: '-1', 'aria-labelledby': 'cf-title' },
      el('div', { class: 'offcanvas-header' },
        el('h2', { class: 'offcanvas-title h5 fw-bold', id: 'cf-title' }, editing ? 'Ubah customer' : 'Customer baru'),
        el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'offcanvas', 'aria-label': 'Tutup' })),
      el('div', { class: 'offcanvas-body pt-0' }, form));
    document.body.append(sheet);
    const offcanvas = new bootstrap.Offcanvas(sheet);

    const close = (value) => { result = value; offcanvas.hide(); };

    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      clearFieldErrors(form);
      conflict.classList.add('d-none');
      if (!form.checkValidity()) {
        form.classList.add('was-validated');
        return;
      }
      await withBusy(save, async () => {
        try {
          const body = formData(form);
          const saved = editing
            ? await api(`/api/customers/${customer.id}`, { method: 'PUT', body })
            : await api('/api/customers', { method: 'POST', body });
          close(saved);
        } catch (error) {
          if (error instanceof ApiError && error.status === 409 && error.problem.customerId) {
            const existingId = error.problem.customerId;
            conflict.replaceChildren(icon('bi-person-exclamation', 'me-1'), error.message, ' ',
              el('button', {
                type: 'button', class: 'btn btn-sm btn-warning mt-2 d-block',
                onclick: async () => close(await api(`/api/customers/${existingId}`)),
              }, 'Pakai customer yang sudah ada'));
            conflict.classList.remove('d-none');
          } else if (error instanceof ApiError && error.status === 400) {
            showFieldErrors(form, error);
          } else {
            conflict.textContent = error.message;
            conflict.classList.remove('d-none');
          }
        }
      });
    });

    sheet.addEventListener('shown.bs.offcanvas', () => (form.name.value ? form.phone : form.name).focus());
    sheet.addEventListener('hidden.bs.offcanvas', () => { sheet.remove(); resolve(result); });
    offcanvas.show();
  });
}
