// Small UI helpers shared by every page.

/**
 * Creates an element. Text children become text nodes (safe: never parsed as HTML).
 *   el('a', { class: 'btn', href: '/x', onclick: fn }, 'Label', otherElement)
 */
export function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs ?? {})) {
    if (value === undefined || value === null || value === false) continue;
    if (key.startsWith('on') && typeof value === 'function') node.addEventListener(key.slice(2), value);
    else if (key === 'class') node.className = value;
    else if (key === 'dataset') Object.assign(node.dataset, value);
    else if (value === true) node.setAttribute(key, '');
    else node.setAttribute(key, value);
  }
  for (const child of children.flat()) {
    if (child === undefined || child === null || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

export const icon = (name, extra = '') => el('i', { class: `bi ${name} ${extra}`.trim(), 'aria-hidden': 'true' });

export const $ = (selector, root = document) => root.querySelector(selector);
export const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

export function debounce(fn, ms = 250) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

function toastContainer() {
  let container = $('#toasts');
  if (!container) {
    container = el('div', { id: 'toasts', class: 'toast-container position-fixed top-0 start-50 translate-middle-x p-3', 'aria-live': 'polite' });
    document.body.append(container);
  }
  return container;
}

/** Short message at the top of the screen. type: success | danger | info */
export function toast(message, type = 'success') {
  const icons = { success: 'bi-check-circle-fill', danger: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
  const node = el('div', { class: `toast align-items-center border-0 text-bg-${type === 'info' ? 'dark' : type}`, role: 'status' },
    el('div', { class: 'd-flex' },
      el('div', { class: 'toast-body d-flex align-items-center gap-2' }, icon(icons[type] ?? icons.info), message),
      el('button', { type: 'button', class: 'btn-close btn-close-white me-2 m-auto', 'data-bs-dismiss': 'toast', 'aria-label': 'Tutup' })));
  toastContainer().append(node);
  const instance = bootstrap.Toast.getOrCreateInstance(node, { delay: type === 'danger' ? 6000 : 3000 });
  node.addEventListener('hidden.bs.toast', () => node.remove());
  instance.show();
}

/** Asks for confirmation in a modal. Resolves true/false. */
export function confirmDialog(message, { title = 'Konfirmasi', okText = 'Ya', danger = false } = {}) {
  return new Promise((resolve) => {
    let answer = false;
    const ok = el('button', { type: 'button', class: `btn ${danger ? 'btn-danger' : 'btn-primary'}` }, okText);
    const modalEl = el('div', { class: 'modal fade', tabindex: '-1' },
      el('div', { class: 'modal-dialog modal-dialog-centered' },
        el('div', { class: 'modal-content' },
          el('div', { class: 'modal-body p-4' },
            el('h2', { class: 'h5 fw-bold mb-2' }, title),
            el('p', { class: 'mb-0 muted' }, message)),
          el('div', { class: 'modal-footer border-0 pt-0' },
            el('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Batal'),
            ok))));
    document.body.append(modalEl);
    const modal = new bootstrap.Modal(modalEl);
    ok.addEventListener('click', () => { answer = true; modal.hide(); });
    modalEl.addEventListener('hidden.bs.modal', () => { modalEl.remove(); resolve(answer); });
    modal.show();
  });
}

/** Disables a button and shows a spinner while `task` runs. */
export async function withBusy(button, task) {
  const original = [...button.childNodes];
  button.disabled = true;
  button.replaceChildren(el('span', { class: 'spinner-border spinner-border-sm me-2', 'aria-hidden': 'true' }), 'Menyimpan…');
  try {
    return await task();
  } finally {
    button.disabled = false;
    button.replaceChildren(...original);
  }
}

/** Shows 400 validation messages under the matching inputs (name="phone" ↔ errors.phone). */
export function showFieldErrors(form, error) {
  clearFieldErrors(form);
  const unmatched = [];
  for (const [field, messages] of Object.entries(error.fieldErrors ?? {})) {
    const input = form.querySelector(`[name="${CSS.escape(field)}"]`);
    if (!input) {
      unmatched.push(...messages);
      continue;
    }
    input.classList.add('is-invalid');
    const feedback = el('div', { class: 'invalid-feedback', 'data-generated': 'true' }, messages.join(' '));
    (input.closest('.input-group') ?? input).after(feedback);
  }
  const first = form.querySelector('.is-invalid');
  first?.focus();
  if (!first) toast(unmatched.join(' ') || error.message, 'danger');
}

export function clearFieldErrors(form) {
  $$('.is-invalid', form).forEach((input) => input.classList.remove('is-invalid'));
  $$('[data-generated]', form).forEach((node) => node.remove());
}

export function emptyState(iconName, title, text, action) {
  return el('div', { class: 'empty' }, icon(iconName), el('div', { class: 'title' }, title), text ? el('div', { class: 'small mt-1' }, text) : null, action ? el('div', { class: 'mt-3' }, action) : null);
}

export function skeletonRows(count = 4) {
  return el('div', { class: 'list-card' }, Array.from({ length: count }, () =>
    el('div', { class: 'list-row' },
      el('div', { class: 'avatar skeleton' }),
      el('div', { class: 'grow' }, el('div', { class: 'skeleton mb-2', style: 'height:0.9rem;width:55%' }), el('div', { class: 'skeleton', style: 'height:0.75rem;width:35%' })))));
}

export function badge(info, extra = '') {
  return el('span', { class: `badge badge-status ${info.badge} ${extra}`.trim() }, icon(info.icon, 'me-1'), info.label);
}

export function formData(form) {
  return Object.fromEntries(new FormData(form).entries());
}

/** Shows an error message in place of the page content. */
export function pageError(container, error, retry) {
  container.replaceChildren(emptyState('bi-wifi-off', 'Gagal memuat data', error.message,
    retry ? el('button', { class: 'btn btn-outline-primary', onclick: retry }, icon('bi-arrow-clockwise', 'me-1'), 'Coba lagi') : null));
}
