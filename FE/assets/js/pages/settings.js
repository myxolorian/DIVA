import { api, ApiError } from '../api.js';
import { startPage } from '../layout.js';
import { logout, sessionEmail } from '../auth.js';
import { $, toast, withBusy, showFieldErrors, clearFieldErrors, formData } from '../ui.js';

const form = $('#outlet-form');
const save = $('#save');

const me = await startPage('settings');
$('#account-email').textContent = me?.email ?? sessionEmail() ?? '—';
$('#logout').addEventListener('click', logout);

try {
  const outlet = await api('/api/outlet');
  for (const key of ['name', 'address', 'phone', 'receiptFooter']) form[key].value = outlet[key] ?? '';
} catch (error) {
  toast(error.message, 'danger');
}

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  clearFieldErrors(form);
  if (!form.checkValidity()) {
    form.classList.add('was-validated');
    return;
  }
  await withBusy(save, async () => {
    try {
      const outlet = await api('/api/outlet', { method: 'PUT', body: formData(form) });
      form.phone.value = outlet.phone ?? '';
      toast('Profil laundry tersimpan.');
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) showFieldErrors(form, error);
      else toast(error.message, 'danger');
    }
  });
});
