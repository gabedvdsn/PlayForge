// PlayForge's additions to DocFX's modern template. DocFX imports this module on every page and runs `start` as the
// page loads, before it renders the page's diagrams.
//
// Diagrams: Mermaid fits a diagram to the text column, which shrinks a wide one until its words can't be read. Here a
// diagram is drawn at no less than MIN_SCALE of its own size, scrolls sideways where that's wider than the column, and
// opens full-screen to pan and zoom, from its Enlarge button or a click on it.

// The smallest a diagram is drawn at in the page, of its own size
const MIN_SCALE = 0.6;

// The smallest the viewer opens a diagram at, where the whole of it doesn't fit the window larger
const READ_SCALE = 0.8;

const PADDING = 12;

function sizeOf(svg) {
  const box = svg.viewBox && svg.viewBox.baseVal;
  return box && box.width > 0 ? { width: box.width, height: box.height } : null;
}

// The diagram as wide as the column, unless that's below MIN_SCALE of its size: then at MIN_SCALE, scrolling sideways
function fitInPage(svg) {
  const size = sizeOf(svg);
  const pre = svg.parentElement;
  if (!size || !pre) return;
  const column = pre.clientWidth - 2 * PADDING;
  if (column <= 0) return;
  const width = Math.max(Math.min(size.width, column), size.width * MIN_SCALE);
  svg.style.maxWidth = 'none';
  svg.style.width = `${Math.round(width)}px`;
  svg.style.height = 'auto';
}

// The diagram's frame: its own scroll, and the Enlarge button, which stays in the frame's corner as the diagram scrolls
function frame(pre) {
  if (pre.parentElement && pre.parentElement.classList.contains('pf-diagram')) return;
  const holder = document.createElement('div');
  holder.className = 'pf-diagram';
  pre.parentNode.insertBefore(holder, pre);
  holder.appendChild(pre);

  const enlarge = document.createElement('button');
  enlarge.type = 'button';
  enlarge.className = 'pf-diagram__enlarge';
  enlarge.title = 'Enlarge the diagram';
  enlarge.setAttribute('aria-label', 'Enlarge the diagram');
  enlarge.innerHTML = '<i class="bi bi-arrows-fullscreen" aria-hidden="true"></i>';
  enlarge.addEventListener('click', () => open(pre, enlarge));
  holder.appendChild(enlarge);

  // A click enlarges it too, unless it ended a text selection
  pre.addEventListener('click', () => {
    if (!String(window.getSelection() || '')) open(pre, enlarge);
  });
}

// The full-screen viewer: the diagram at its own size, panned by a drag and zoomed by the wheel or the toolbar
function open(pre, returnTo) {
  const svg = pre.querySelector('svg');
  const size = svg && sizeOf(svg);
  if (!size) return;

  const viewer = document.createElement('div');
  viewer.className = 'pf-viewer';
  viewer.setAttribute('role', 'dialog');
  viewer.setAttribute('aria-modal', 'true');
  viewer.setAttribute('aria-label', 'Diagram');
  viewer.innerHTML =
    '<div class="pf-viewer__bar">' +
    '<button type="button" data-act="out" title="Zoom out (-)" aria-label="Zoom out"><i class="bi bi-zoom-out" aria-hidden="true"></i></button>' +
    '<button type="button" data-act="in" title="Zoom in (+)" aria-label="Zoom in"><i class="bi bi-zoom-in" aria-hidden="true"></i></button>' +
    '<button type="button" data-act="fit" title="Fit to the window (0)">Fit</button>' +
    '<button type="button" data-act="actual" title="Actual size (1)">100%</button>' +
    '<span class="pf-viewer__hint">Scroll to zoom, drag to move</span>' +
    '<button type="button" data-act="close" title="Close (Esc)" aria-label="Close"><i class="bi bi-x-lg" aria-hidden="true"></i></button>' +
    '</div>' +
    '<div class="pf-viewer__stage"><div class="pf-viewer__layer"></div></div>';
  const stage = viewer.querySelector('.pf-viewer__stage');
  const layer = viewer.querySelector('.pf-viewer__layer');

  // A copy keeps the diagram's id, which its own styles select by
  const copy = svg.cloneNode(true);
  copy.setAttribute('style', `width:${size.width}px;height:${size.height}px;max-width:none;`);
  layer.appendChild(copy);

  const overflow = document.body.style.overflow;
  document.body.appendChild(viewer);
  document.body.style.overflow = 'hidden';

  let scale = 1;
  let x = 0;
  let y = 0;
  const apply = () => {
    layer.style.transform = `translate(${x}px, ${y}px) scale(${scale})`;
  };
  const bounds = () => stage.getBoundingClientRect();
  const zoomAt = (factor, px, py) => {
    const next = Math.min(4, Math.max(0.05, scale * factor));
    x = px - (px - x) * (next / scale);
    y = py - (py - y) * (next / scale);
    scale = next;
    apply();
  };
  const zoomCentre = factor => {
    const r = bounds();
    zoomAt(factor, r.width / 2, r.height / 2);
  };
  const fit = () => {
    const r = bounds();
    scale = Math.min((r.width - 32) / size.width, (r.height - 32) / size.height, 1);
    x = (r.width - size.width * scale) / 2;
    y = (r.height - size.height * scale) / 2;
    apply();
  };
  const actual = () => {
    const r = bounds();
    scale = 1;
    x = Math.max(16, (r.width - size.width) / 2);
    y = 16;
    apply();
  };
  // Whole, where the whole of it fits the window at a readable size; else its top, as wide as the window allows but
  // no smaller than READ_SCALE
  const first = () => {
    const r = bounds();
    const whole = Math.min((r.width - 32) / size.width, (r.height - 32) / size.height);
    if (whole >= READ_SCALE) {
      fit();
      return;
    }
    scale = Math.min(1, Math.max(READ_SCALE, (r.width - 32) / size.width));
    x = Math.max(16, (r.width - size.width * scale) / 2);
    y = 16;
    apply();
  };

  stage.addEventListener('wheel', event => {
    event.preventDefault();
    const r = bounds();
    zoomAt(Math.exp(-event.deltaY * 0.0015), event.clientX - r.left, event.clientY - r.top);
  }, { passive: false });

  let drag = null;
  stage.addEventListener('pointerdown', event => {
    drag = { id: event.pointerId, from: [event.clientX, event.clientY], at: [x, y] };
    stage.setPointerCapture(event.pointerId);
    stage.classList.add('is-dragging');
  });
  stage.addEventListener('pointermove', event => {
    if (!drag || drag.id !== event.pointerId) return;
    x = drag.at[0] + event.clientX - drag.from[0];
    y = drag.at[1] + event.clientY - drag.from[1];
    apply();
  });
  const endDrag = event => {
    if (!drag || drag.id !== event.pointerId) return;
    drag = null;
    stage.classList.remove('is-dragging');
  };
  stage.addEventListener('pointerup', endDrag);
  stage.addEventListener('pointercancel', endDrag);

  const close = () => {
    document.removeEventListener('keydown', keys, true);
    viewer.remove();
    document.body.style.overflow = overflow;
    if (returnTo) returnTo.focus();
  };
  const actions = {
    in: () => zoomCentre(1.25),
    out: () => zoomCentre(0.8),
    fit,
    actual,
    close,
  };
  viewer.querySelector('.pf-viewer__bar').addEventListener('click', event => {
    const button = event.target.closest('button[data-act]');
    if (button) actions[button.dataset.act]();
  });
  function keys(event) {
    const act = { Escape: 'close', '+': 'in', '=': 'in', '-': 'out', '0': 'fit', '1': 'actual' }[event.key];
    if (!act) return;
    event.preventDefault();
    event.stopPropagation();
    actions[act]();
  }
  document.addEventListener('keydown', keys, true);

  first();
  viewer.querySelector('[data-act="close"]').focus();
}

// Each diagram Mermaid has drawn, framed once and sized each time: Mermaid draws a diagram again as the theme changes
function enhance() {
  document.querySelectorAll('pre.mermaid > svg').forEach(svg => {
    frame(svg.parentElement);
    fitInPage(svg);
  });
}

export default {
  start: () => {
    // Batched by a timer, not an animation frame, which a page in a background tab doesn't get
    let queued = false;
    const later = () => {
      if (queued) return;
      queued = true;
      setTimeout(() => {
        queued = false;
        enhance();
      }, 30);
    };
    new MutationObserver(later).observe(document.body, { childList: true, subtree: true });
    window.addEventListener('resize', later);
    later();
  },
};
