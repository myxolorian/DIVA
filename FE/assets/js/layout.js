// The frame around every logged-in page: sidebar on wide screens, bottom bar on phones,
// plus the login check. Each page calls startPage('dashboard') first.
import { api } from './api.js';
import { goToLogin } from './auth.js';
import { el, icon } from './ui.js';

const items = [
  { key: 'dashboard', href: '/', icon: 'bi-house', label: 'Beranda' },
  { key: 'orders', href: '/orders.html', icon: 'bi-receipt', label: 'Order' },
  { key: 'customers', href: '/customers.html', icon: 'bi-people', label: 'Customer' },
  { key: 'services', href: '/services.html', icon: 'bi-tags', label: 'Jasa & Harga' },
  { key: 'settings', href: '/settings.html', icon: 'bi-gear', label: 'Pengaturan' },
];

function sidebar(active) {
  return el('nav', { class: 'sidebar', 'aria-label': 'Menu utama' },
    el('div', { class: 'brand' }, 'DIVA', el('span', {}, '.'), el('div', { class: 'small fw-semibold muted', style: 'letter-spacing:0' }, 'Laundry Management')),
    el('a', { class: 'btn btn-primary new-order', href: '/order-new.html' }, icon('bi-plus-lg', 'me-1'), 'Buat Order'),
    items.map((item) => el('a', { class: `nav-item${item.key === active ? ' active' : ''}`, href: item.href, 'aria-current': item.key === active ? 'page' : null }, icon(item.icon), item.label)));
}

function bottomNav(active) {
  const link = (item) => el('a', { href: item.href, class: item.key === active ? 'active' : '', 'aria-current': item.key === active ? 'page' : null }, icon(item.icon), item.label);
  const more = active === 'services' || active === 'settings';
  return el('nav', { class: 'bottom-nav', 'aria-label': 'Menu utama' },
    link(items[0]),
    link(items[1]),
    el('a', { href: '/order-new.html', 'aria-label': 'Buat order baru' }, el('span', { class: 'fab' }, icon('bi-plus-lg'))),
    link(items[2]),
    el('a', { href: '/settings.html', class: more ? 'active' : '' }, icon('bi-three-dots'), 'Lainnya'));
}

/** Checks the login, draws the navigation and returns the current user ({ id, email }). */
export async function startPage(active) {
  document.body.prepend(sidebar(active), bottomNav(active));
  try {
    return await api('/api/me');
  } catch (error) {
    if (error.status !== 401) {
      // Server unreachable: keep the page, it will show its own error message.
      return null;
    }
    goToLogin();
    return new Promise(() => {}); // never continue on this page
  }
}
