import { getConfig, login } from '../auth.js';
import { $ } from '../ui.js';

const form = $('#login-form');
const errorBox = $('#login-error');
const button = $('#login-button');

// Only same-site paths are accepted as "next", so the login page cannot be used to
// send someone to another website after they log in.
function nextPage() {
  const next = new URLSearchParams(location.search).get('next');
  return next && next.startsWith('/') && !next.startsWith('//') ? next : '/';
}

getConfig().then((config) => {
  if (config.devBypass) $('#dev-notice').classList.remove('d-none');
}).catch(() => {});

$('#toggle-password').addEventListener('click', (event) => {
  const input = $('#password');
  const show = input.type === 'password';
  input.type = show ? 'text' : 'password';
  event.currentTarget.firstElementChild.className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
});

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  errorBox.classList.add('d-none');
  if (!form.checkValidity()) {
    form.classList.add('was-validated');
    return;
  }
  button.disabled = true;
  button.textContent = 'Memeriksa…';
  try {
    await login(form.email.value.trim(), form.password.value);
    location.replace(nextPage());
  } catch (error) {
    errorBox.textContent = error.message;
    errorBox.classList.remove('d-none');
    button.disabled = false;
    button.textContent = 'Masuk';
  }
});
