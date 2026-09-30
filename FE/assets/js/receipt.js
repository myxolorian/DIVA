// Public receipt page: /r/{token}. No login needed; the token in the URL is the key.
// All data is written with textContent (never innerHTML), so a customer named
// "<script>..." shows up as plain text instead of running as code.

const token = decodeURIComponent(location.pathname.split('/').filter(Boolean).pop() ?? '');
const apiUrl = `/api/public/receipts/${encodeURIComponent(token)}`;

const rupiah = new Intl.NumberFormat('id-ID', { style: 'currency', currency: 'IDR', minimumFractionDigits: 0, maximumFractionDigits: 2 });
const number = new Intl.NumberFormat('id-ID', { maximumFractionDigits: 2 });
const dateTime = new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Jakarta' });
const dateOnly = new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium', timeZone: 'UTC' });

const units = { Kg: 'kg', Pcs: 'pcs', M2: 'm²' };
const statuses = { Baru: 'Baru', Diproses: 'Diproses', Selesai: 'Selesai', Diambil: 'Sudah diambil' };

function setField(name, value) {
  for (const el of document.querySelectorAll(`[data-field="${name}"]`)) {
    el.textContent = value ?? '';
  }
  // Hide the label of an optional field that has no value.
  for (const el of document.querySelectorAll(`[data-show="${name}"]`)) {
    el.classList.toggle('d-none', !value);
  }
}

function quantity(item) {
  const unit = units[item.unit] ?? item.unit;
  const text = `${number.format(item.qty)} ${unit}`;
  return item.billedQty > item.qty ? `${text} (min. ${number.format(item.billedQty)} ${unit})` : text;
}

function renderItems(items) {
  const list = document.getElementById('items');
  list.replaceChildren(...items.map((item) => {
    const li = document.createElement('li');
    li.className = 'receipt-line';

    const name = document.createElement('div');
    name.className = 'fw-semibold';
    name.textContent = item.serviceName;

    const row = document.createElement('div');
    row.className = 'd-flex justify-content-between small';
    const detail = document.createElement('span');
    detail.className = 'text-secondary';
    detail.textContent = `${quantity(item)} × ${rupiah.format(item.unitPrice)}`;
    const subtotal = document.createElement('span');
    subtotal.textContent = rupiah.format(item.subtotal);
    row.append(detail, subtotal);

    li.append(name, row);
    return li;
  }));
}

function render(r) {
  document.title = `Receipt ${r.orderNumber} - ${r.outlet.name}`;
  setField('outletName', r.outlet.name);
  setField('outletAddress', r.outlet.address);
  setField('outletPhone', r.outlet.phone ? `Telp. ${r.outlet.phone}` : '');
  setField('orderNumber', r.orderNumber);
  setField('createdAt', `${dateTime.format(new Date(r.createdAt))} WIB`);
  setField('dueDate', r.dueDate ? dateOnly.format(new Date(r.dueDate)) : '');
  setField('customerName', r.customerName);
  setField('customerPhone', r.customerPhone);
  setField('total', rupiah.format(r.total));
  setField('status', statuses[r.status] ?? r.status);
  setField('notes', r.notes);
  setField('footer', r.outlet.footer);
  renderItems(r.items);

  const paid = r.paymentStatus === 'Lunas';
  const badge = document.getElementById('payment');
  badge.textContent = paid ? 'LUNAS' : 'BELUM LUNAS';
  badge.classList.add(paid ? 'text-bg-success' : 'text-bg-warning');

  document.getElementById('pdf').href = r.pdfPath;
  document.getElementById('status').classList.add('d-none');
  document.getElementById('receipt').classList.remove('d-none');
}

function showMessage(text) {
  const status = document.getElementById('status');
  status.textContent = text;
  status.classList.remove('d-none');
  document.getElementById('receipt').classList.add('d-none');
}

async function copyLink(button) {
  try {
    await navigator.clipboard.writeText(location.href);
    button.textContent = 'Link disalin ✓';
  } catch {
    // Clipboard needs HTTPS (or localhost); show the link so it can be copied by hand.
    prompt('Salin link receipt:', location.href);
  }
}

async function share(orderNumber) {
  const text = `Receipt DIVA Laundry ${orderNumber}`;
  if (navigator.share) {
    try {
      await navigator.share({ title: text, text, url: location.href });
    } catch {
      // The user closed the share sheet: nothing to do.
    }
    return;
  }
  // Desktop browsers without Web Share: fall back to WhatsApp.
  window.open(`https://wa.me/?text=${encodeURIComponent(`${text}: ${location.href}`)}`, '_blank', 'noopener');
}

async function load() {
  try {
    const response = await fetch(apiUrl, { headers: { Accept: 'application/json' } });
    if (response.status === 404) {
      showMessage('Receipt tidak ditemukan. Periksa kembali link yang Anda terima.');
      return;
    }
    if (response.status === 429) {
      showMessage('Terlalu banyak permintaan. Coba lagi dalam satu menit.');
      return;
    }
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const receipt = await response.json();
    render(receipt);
    document.getElementById('copy').addEventListener('click', (e) => copyLink(e.currentTarget));
    document.getElementById('share').addEventListener('click', () => share(receipt.orderNumber));
  } catch {
    showMessage('Receipt gagal dimuat. Periksa koneksi internet lalu muat ulang halaman.');
  }
}

load();
