// Body scroll lock for modal overlays - a lightweight scroll-removal helper.
// Refcounted so stacked overlays (dialog opening a dialog) only restore scrolling when the
// last one releases. Pads the body by the scrollbar width so the page doesn't shift.

let count = 0;
let prevOverflow = '';
let prevPaddingRight = '';

export function lock(): void {
  if (count++ > 0) return;

  const scrollbar = window.innerWidth - document.documentElement.clientWidth;
  prevOverflow = document.body.style.overflow;
  prevPaddingRight = document.body.style.paddingRight;

  document.body.style.overflow = 'hidden';
  // With `scrollbar-gutter: stable` on the root the gutter stays reserved while scrolling is
  // off, so the classic padding compensation would double-shift the content - skip it there.
  const gutterStable = (getComputedStyle(document.documentElement).scrollbarGutter ?? '').includes('stable');
  if (scrollbar > 0 && !gutterStable) {
    const current = parseFloat(getComputedStyle(document.body).paddingRight) || 0;
    document.body.style.paddingRight = `${current + scrollbar}px`;
  }
  document.body.setAttribute('data-scroll-locked', '');
}

export function unlock(): void {
  if (count === 0 || --count > 0) return;

  document.body.style.overflow = prevOverflow;
  document.body.style.paddingRight = prevPaddingRight;
  document.body.removeAttribute('data-scroll-locked');
}
