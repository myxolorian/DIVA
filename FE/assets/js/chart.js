// A small single-series column chart of money per day (plain SVG, no library).
// One colour (the series), no legend (the card title names it), one value label (the last
// day or the best day), and a tooltip on hover/focus for every column. A hidden table keeps
// every value available to screen readers.
import { el } from './ui.js';
import { compact, rupiah, weekday, date } from './format.js';

const SVG = 'http://www.w3.org/2000/svg';
const svg = (tag, attrs = {}) => {
  const node = document.createElementNS(SVG, tag);
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
  return node;
};

function niceMax(value) {
  if (value <= 0) return 1;
  const power = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 2.5, 5, 10].find((m) => m * power >= value);
  return step * power;
}

/** Column with a 4px rounded top and a square base. */
function columnPath(x, y, width, height) {
  const r = Math.min(4, height, width / 2);
  return `M${x},${y + height} V${y + r} Q${x},${y} ${x + r},${y} H${x + width - r} Q${x + width},${y} ${x + width},${y + r} V${y + height} Z`;
}

const MIN_LABEL_SPACE = 28; // px per x-axis label; closer labels are thinned out

/**
 * points: [{ date, orders, total }].
 * options.highlight: 'last' = today stands out and carries the value label (dashboard);
 *                    'max'  = every column the same colour, the best day carries the label.
 * options.label(point, isLast): x-axis text; defaults to the weekday ("Hari ini" for the last day).
 */
export function dailyChart(points, {
  highlight = 'last',
  label = (point, isLast) => (isLast ? 'Hari ini' : weekday(point.date)),
  title = 'Nilai order 7 hari terakhir',
  caption = 'Nilai order per hari',
} = {}) {
  const options = { highlight, label, title };
  const wrap = el('div', { class: 'chart' });
  const tooltip = el('div', { class: 'chart-tooltip d-none', role: 'status' });
  const table = el('table', { class: 'visually-hidden' },
    el('caption', {}, caption),
    el('thead', {}, el('tr', {}, el('th', {}, 'Tanggal'), el('th', {}, 'Order'), el('th', {}, 'Nilai'))),
    el('tbody', {}, points.map((p) => el('tr', {}, el('td', {}, date(p.date)), el('td', {}, p.orders), el('td', {}, rupiah(p.total))))));
  wrap.append(tooltip, table);

  // The SVG is drawn at the card's real pixel width (no viewBox scaling), so the axis text
  // stays 12px on a phone and on a wide screen. It is redrawn when the card changes width.
  let drawnWidth = 0;
  const draw = () => {
    const width = Math.round(wrap.clientWidth);
    if (!width || width === drawnWidth) return;
    drawnWidth = width;
    wrap.querySelector('svg')?.remove();
    tooltip.classList.add('d-none');
    wrap.prepend(render(points, width, tooltip, options));
  };
  new ResizeObserver(draw).observe(wrap);
  return wrap;
}

function render(points, width, tooltip, { highlight, label, title }) {
  const height = 200;
  const pad = { top: 22, right: 4, bottom: 30, left: 44 };
  const plotW = width - pad.left - pad.right;
  const plotH = height - pad.top - pad.bottom;
  const max = niceMax(Math.max(...points.map((p) => p.total)));
  const slot = plotW / points.length;
  const barW = Math.min(24, slot * 0.5);
  const yOf = (v) => pad.top + plotH - (v / max) * plotH;

  const root = svg('svg', { width, height, role: 'img', 'aria-label': title });
  const labelEvery = Math.ceil(MIN_LABEL_SPACE / slot);
  const best = points.reduce((top, p, i) => (p.total > points[top].total ? i : top), 0);
  const labelled = highlight === 'last' ? points.length - 1 : best;

  for (const tick of [0, max / 2, max]) {
    const y = yOf(tick);
    root.append(svg('line', { class: 'grid', x1: pad.left, x2: width - pad.right, y1: y, y2: y }));
    const label = svg('text', { class: 'axis-label', x: pad.left - 8, y: y + 4, 'text-anchor': 'end' });
    label.textContent = tick === 0 ? '0' : compact(tick);
    root.append(label);
  }

  points.forEach((point, i) => {
    const cx = pad.left + slot * i + slot / 2;
    const isLast = i === points.length - 1;

    // Thinned from the end, so the most recent day always has its label.
    if ((points.length - 1 - i) % labelEvery === 0) {
      const xLabel = svg('text', { class: 'axis-label', x: cx, y: height - 8, 'text-anchor': 'middle' });
      xLabel.textContent = label(point, isLast);
      root.append(xLabel);
    }

    // Hit area: the whole column slot, larger than the bar, focusable with the keyboard.
    const hit = svg('rect', { class: 'hit', x: cx - slot / 2, y: pad.top, width: slot, height: plotH, tabindex: 0,
      'aria-label': `${date(point.date)}: ${rupiah(point.total)}, ${point.orders} order` });
    root.append(hit);

    if (point.total > 0) {
      const top = yOf(point.total);
      const dim = highlight === 'last' && !isLast;
      root.append(svg('path', { class: `bar${dim ? ' dim' : ''}`, d: columnPath(cx - barW / 2, top, barW, pad.top + plotH - top) }));
      if (i === labelled) {
        const value = svg('text', { class: 'value-label', x: cx, y: top - 6, 'text-anchor': 'middle' });
        value.textContent = compact(point.total);
        root.append(value);
      }
    }

    const show = () => {
      tooltip.replaceChildren(el('strong', {}, rupiah(point.total)), `${point.orders} order · ${date(point.date)}`);
      tooltip.style.top = `${Math.min(yOf(point.total), pad.top + plotH)}px`;
      tooltip.classList.remove('d-none');
      // Centre over the column, but keep the whole tooltip inside the chart (first/last day).
      const half = tooltip.offsetWidth / 2;
      tooltip.style.left = `${Math.min(Math.max(cx, half), width - half)}px`;
    };
    const hide = () => tooltip.classList.add('d-none');
    hit.addEventListener('pointerenter', show);
    hit.addEventListener('pointerleave', hide);
    hit.addEventListener('focus', show);
    hit.addEventListener('blur', hide);
  });

  return root;
}
