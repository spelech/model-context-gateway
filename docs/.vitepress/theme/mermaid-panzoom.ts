/* Model Context Gateway (MCG) - VitePress Mermaid Pan & Zoom Controller */

const ZOOM_FACTOR = 1.25;
const MIN_SCALE = 0.2;
const MAX_SCALE = 10;

function setupDiagram(container: HTMLElement): void {
  if (container.dataset.panzoomReady === 'true') return;
  const svg = container.querySelector('svg');
  if (!svg) return;

  container.dataset.panzoomReady = 'true';

  let scale = 1;
  let panX = 0;
  let panY = 0;
  let isDragging = false;
  let startX = 0;
  let startY = 0;
  let isFullscreen = false;

  svg.style.transformOrigin = 'center center';
  svg.style.willChange = 'transform';

  function updateTransform(animate: boolean): void {
    if (!svg) return;
    svg.style.transition = animate ? 'transform 0.15s ease-out' : 'none';
    svg.style.transform = `translate(${panX}px, ${panY}px) scale(${scale})`;
  }

  function resetView(): void {
    scale = 1;
    panX = 0;
    panY = 0;
    updateTransform(true);
  }

  function zoom(deltaFactor: number, focalX?: number, focalY?: number): void {
    const prevScale = scale;
    scale = Math.min(Math.max(scale * deltaFactor, MIN_SCALE), MAX_SCALE);
    if (scale === prevScale) return;

    if (focalX !== undefined && focalY !== undefined) {
      const rect = container.getBoundingClientRect();
      const offsetX = focalX - (rect.left + rect.width / 2);
      const offsetY = focalY - (rect.top + rect.height / 2);
      panX -= offsetX * (deltaFactor - 1);
      panY -= offsetY * (deltaFactor - 1);
    }
    updateTransform(true);
  }

  function toggleFullscreen(): void {
    isFullscreen = !isFullscreen;
    if (isFullscreen) {
      container.classList.add('mermaid-fullscreen');
      document.body.classList.add('has-mermaid-fullscreen');
    } else {
      container.classList.remove('mermaid-fullscreen');
      document.body.classList.remove('has-mermaid-fullscreen');
    }
    resetView();
  }

  // Floating toolbar
  const toolbar = document.createElement('div');
  toolbar.className = 'mermaid-toolbar';
  toolbar.setAttribute('role', 'toolbar');
  toolbar.setAttribute('aria-label', 'Mermaid Diagram Controls');

  function createBtn(title: string, iconSvg: string, onClick: () => void): HTMLButtonElement {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'mermaid-toolbar-btn';
    btn.title = title;
    btn.setAttribute('aria-label', title);
    btn.innerHTML = iconSvg;
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      e.preventDefault();
      onClick();
    });
    return btn;
  }

  toolbar.appendChild(
    createBtn(
      'Zoom In (+)',
      `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line><line x1="11" y1="8" x2="11" y2="14"></line><line x1="8" y1="11" x2="14" y2="11"></line></svg>`,
      () => zoom(ZOOM_FACTOR)
    )
  );

  toolbar.appendChild(
    createBtn(
      'Zoom Out (-)',
      `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line><line x1="8" y1="11" x2="14" y2="11"></line></svg>`,
      () => zoom(1 / ZOOM_FACTOR)
    )
  );

  toolbar.appendChild(
    createBtn(
      'Reset View',
      `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><polyline points="3 3 3 8 8 8"></polyline></svg>`,
      () => resetView()
    )
  );

  toolbar.appendChild(
    createBtn(
      'Toggle Fullscreen',
      `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>`,
      () => toggleFullscreen()
    )
  );

  container.appendChild(toolbar);

  // Mouse pan
  container.addEventListener('mousedown', (e: MouseEvent) => {
    if ((e.target as HTMLElement).closest('.mermaid-toolbar')) return;
    isDragging = true;
    startX = e.clientX - panX;
    startY = e.clientY - panY;
    container.classList.add('is-dragging');
  });

  window.addEventListener('mousemove', (e: MouseEvent) => {
    if (!isDragging) return;
    panX = e.clientX - startX;
    panY = e.clientY - startY;
    updateTransform(false);
  });

  window.addEventListener('mouseup', () => {
    if (isDragging) {
      isDragging = false;
      container.classList.remove('is-dragging');
      updateTransform(true);
    }
  });

  // Wheel zoom
  container.addEventListener(
    'wheel',
    (e: WheelEvent) => {
      e.preventDefault();
      const delta = e.deltaY < 0 ? ZOOM_FACTOR : 1 / ZOOM_FACTOR;
      zoom(delta, e.clientX, e.clientY);
    },
    { passive: false }
  );

  // Double click reset
  container.addEventListener('dblclick', (e: MouseEvent) => {
    if ((e.target as HTMLElement).closest('.mermaid-toolbar')) return;
    resetView();
  });

  // Mobile Touch Gestures
  let initialPinchDist: number | null = null;
  let initialPinchScale = 1;

  container.addEventListener('touchstart', (e: TouchEvent) => {
    if (e.touches.length === 1) {
      isDragging = true;
      startX = e.touches[0].clientX - panX;
      startY = e.touches[0].clientY - panY;
    } else if (e.touches.length === 2) {
      isDragging = false;
      initialPinchDist = Math.hypot(
        e.touches[0].clientX - e.touches[1].clientX,
        e.touches[0].clientY - e.touches[1].clientY
      );
      initialPinchScale = scale;
    }
  });

  container.addEventListener(
    'touchmove',
    (e: TouchEvent) => {
      if (e.touches.length === 1 && isDragging) {
        e.preventDefault();
        panX = e.touches[0].clientX - startX;
        panY = e.touches[0].clientY - startY;
        updateTransform(false);
      } else if (e.touches.length === 2 && initialPinchDist) {
        e.preventDefault();
        const dist = Math.hypot(
          e.touches[0].clientX - e.touches[1].clientX,
          e.touches[0].clientY - e.touches[1].clientY
        );
        const ratio = dist / initialPinchDist;
        scale = Math.min(Math.max(initialPinchScale * ratio, MIN_SCALE), MAX_SCALE);
        updateTransform(false);
      }
    },
    { passive: false }
  );

  container.addEventListener('touchend', (e: TouchEvent) => {
    if (e.touches.length < 2) initialPinchDist = null;
    if (e.touches.length === 0) isDragging = false;
  });

  // ESC to exit fullscreen
  window.addEventListener('keydown', (e: KeyboardEvent) => {
    if (e.key === 'Escape' && isFullscreen) {
      toggleFullscreen();
    }
  });
}

export function initMermaidPanZoom(): void {
  if (typeof window === 'undefined') return;

  function scan(): void {
    const containers = document.querySelectorAll<HTMLElement>('.mermaid');
    containers.forEach((container) => {
      if (container.querySelector('svg') && container.dataset.panzoomReady !== 'true') {
        setupDiagram(container);
      }
    });
  }

  scan();
  setInterval(scan, 500);
}
