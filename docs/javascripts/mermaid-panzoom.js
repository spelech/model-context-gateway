/* Model Context Gateway (MCG) - Mermaid Pan & Zoom Engine */

(function () {
  "use strict";

  // 1. Intercept Element.prototype.attachShadow so shadow roots are accessible
  const mermaidShadowRoots = new WeakMap();
  const origAttachShadow = Element.prototype.attachShadow;

  Element.prototype.attachShadow = function (init) {
    const root = origAttachShadow.call(this, { ...init, mode: "open" });
    mermaidShadowRoots.set(this, root);
    return root;
  };

  const ZOOM_FACTOR = 1.25;
  const MIN_SCALE = 0.2;
  const MAX_SCALE = 10;

  function initPanZoom(mermaidContainer) {
    if (mermaidContainer.dataset.panzoomInit === "true") return;

    // Retrieve shadow root or regular DOM
    const shadowRoot = mermaidContainer.shadowRoot || mermaidShadowRoots.get(mermaidContainer);
    const svg = (shadowRoot ? shadowRoot.querySelector("svg") : null) || mermaidContainer.querySelector("svg");
    if (!svg) return;

    mermaidContainer.dataset.panzoomInit = "true";

    // Create wrapper if not already wrapped
    let wrapper = mermaidContainer.closest(".mermaid-panzoom-wrapper");
    if (!wrapper) {
      wrapper = document.createElement("div");
      wrapper.className = "mermaid-panzoom-wrapper";
      mermaidContainer.parentNode.insertBefore(wrapper, mermaidContainer);
      wrapper.appendChild(mermaidContainer);
    }

    // State
    let scale = 1;
    let panX = 0;
    let panY = 0;
    let isDragging = false;
    let startX = 0;
    let startY = 0;
    let isFullscreen = false;

    // Style SVG
    svg.style.transformOrigin = "center center";
    svg.style.willChange = "transform";

    function updateTransform(animate) {
      svg.style.transition = animate ? "transform 0.15s ease-out" : "none";
      svg.style.transform = `translate(${panX}px, ${panY}px) scale(${scale})`;
    }

    function resetView() {
      scale = 1;
      panX = 0;
      panY = 0;
      updateTransform(true);
    }

    function zoom(deltaFactor, focalX, focalY) {
      const prevScale = scale;
      scale = Math.min(Math.max(scale * deltaFactor, MIN_SCALE), MAX_SCALE);
      if (scale === prevScale) return;

      if (focalX !== undefined && focalY !== undefined) {
        const rect = wrapper.getBoundingClientRect();
        const offsetX = focalX - (rect.left + rect.width / 2);
        const offsetY = focalY - (rect.top + rect.height / 2);
        panX -= offsetX * (deltaFactor - 1);
        panY -= offsetY * (deltaFactor - 1);
      }
      updateTransform(true);
    }

    function toggleFullscreen() {
      isFullscreen = !isFullscreen;
      if (isFullscreen) {
        wrapper.classList.add("mermaid-fullscreen");
        document.body.classList.add("has-mermaid-fullscreen");
      } else {
        wrapper.classList.remove("mermaid-fullscreen");
        document.body.classList.remove("has-mermaid-fullscreen");
      }
      resetView();
    }

    // Controls Toolbar
    const toolbar = document.createElement("div");
    toolbar.className = "mermaid-toolbar";
    toolbar.setAttribute("role", "toolbar");
    toolbar.setAttribute("aria-label", "Mermaid Diagram Controls");

    function createBtn(title, iconSvg, onClick) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "mermaid-toolbar-btn";
      btn.title = title;
      btn.setAttribute("aria-label", title);
      btn.innerHTML = iconSvg;
      btn.addEventListener("click", (e) => {
        e.stopPropagation();
        e.preventDefault();
        onClick();
      });
      return btn;
    }

    // Zoom In
    toolbar.appendChild(
      createBtn(
        "Zoom In (+)",
        `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line><line x1="11" y1="8" x2="11" y2="14"></line><line x1="8" y1="11" x2="14" y2="11"></line></svg>`,
        () => zoom(ZOOM_FACTOR)
      )
    );

    // Zoom Out
    toolbar.appendChild(
      createBtn(
        "Zoom Out (-)",
        `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line><line x1="8" y1="11" x2="14" y2="11"></line></svg>`,
        () => zoom(1 / ZOOM_FACTOR)
      )
    );

    // Reset View
    toolbar.appendChild(
      createBtn(
        "Reset View",
        `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><polyline points="3 3 3 8 8 8"></polyline></svg>`,
        () => resetView()
      )
    );

    // Fullscreen Toggle
    toolbar.appendChild(
      createBtn(
        "Toggle Fullscreen",
        `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>`,
        () => toggleFullscreen()
      )
    );

    wrapper.appendChild(toolbar);

    // Mouse Drag Panning
    wrapper.addEventListener("mousedown", (e) => {
      if (e.target.closest(".mermaid-toolbar")) return;
      isDragging = true;
      startX = e.clientX - panX;
      startY = e.clientY - panY;
      wrapper.classList.add("is-dragging");
    });

    window.addEventListener("mousemove", (e) => {
      if (!isDragging) return;
      panX = e.clientX - startX;
      panY = e.clientY - startY;
      updateTransform(false);
    });

    window.addEventListener("mouseup", () => {
      if (isDragging) {
        isDragging = false;
        wrapper.classList.remove("is-dragging");
        updateTransform(true);
      }
    });

    // Mouse Wheel Zoom
    wrapper.addEventListener(
      "wheel",
      (e) => {
        e.preventDefault();
        const delta = e.deltaY < 0 ? ZOOM_FACTOR : 1 / ZOOM_FACTOR;
        zoom(delta, e.clientX, e.clientY);
      },
      { passive: false }
    );

    // Double-click to reset
    wrapper.addEventListener("dblclick", (e) => {
      if (e.target.closest(".mermaid-toolbar")) return;
      resetView();
    });

    // Touch Support
    let initialPinchDist = null;
    let initialPinchScale = 1;

    wrapper.addEventListener("touchstart", (e) => {
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

    wrapper.addEventListener(
      "touchmove",
      (e) => {
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

    wrapper.addEventListener("touchend", (e) => {
      if (e.touches.length < 2) initialPinchDist = null;
      if (e.touches.length === 0) isDragging = false;
    });

    // ESC to exit fullscreen
    window.addEventListener("keydown", (e) => {
      if (e.key === "Escape" && isFullscreen) {
        toggleFullscreen();
      }
    });
  }

  function scanAndInit() {
    const containers = document.querySelectorAll(".mermaid");
    containers.forEach((container) => {
      initPanZoom(container);
    });
  }

  // Material for MkDocs instant loading
  if (typeof document$ !== "undefined") {
    document$.subscribe(() => {
      scanAndInit();
    });
  }

  // Dynamic DOM observation
  const observer = new MutationObserver(() => {
    scanAndInit();
  });

  if (document.body) {
    observer.observe(document.body, { childList: true, subtree: true });
  } else {
    document.addEventListener("DOMContentLoaded", () => {
      observer.observe(document.body, { childList: true, subtree: true });
      scanAndInit();
    });
  }

  setInterval(scanAndInit, 1000);
})();
