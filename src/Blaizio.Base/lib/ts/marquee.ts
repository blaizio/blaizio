/**
 * Marquee - overflow measurement for hover-to-reveal truncated text. Wholly DOM-side and pure
 * CSS on the animation end: this module only finds elements marked `data-bz-marquee` under a
 * root, measures whether their text actually overflows, and stamps the results -
 * `data-truncated` plus `--bz-marquee-distance` (how far the text must slide to reveal its tail)
 * and `--bz-marquee-duration` (distance-proportional, so long labels don't fly). The stylesheet
 * (blaizio.css, MARK: Marquee) does the rest on :hover. Labels that fit are left untouched, so
 * only ellipsised text ever moves.
 *
 * Re-measures on resize (per element) and on subtree changes (new nodes - expanded tree
 * branches, virtualized rows), coalesced into one rAF pass.
 */

const SELECTOR = '[data-bz-marquee]';

/** Slide speed: ms per overflowing pixel, clamped so short and long tails both read well. */
const MS_PER_PX = 15;
const MIN_MS = 400;
const MAX_MS = 6000;

class Marquee {
  private readonly resizeObserver: ResizeObserver;
  private readonly mutationObserver: MutationObserver;
  private pending = false;

  constructor(private readonly root: HTMLElement) {
    if (!root) {
      this.resizeObserver = undefined!;
      this.mutationObserver = undefined!;
      return;
    }

    this.resizeObserver = new ResizeObserver(() => this.schedule());
    this.mutationObserver = new MutationObserver(() => this.schedule());
    this.mutationObserver.observe(root, { childList: true, subtree: true, characterData: true });
    this.resizeObserver.observe(root);
    this.schedule();
  }

  private schedule(): void {
    if (this.pending) return;
    this.pending = true;
    const run = () => {
      if (!this.pending) return;
      this.pending = false;
      this.measureAll();
    };
    // rAF coalesces measurement with the next paint, but a hidden or background tab never ticks
    // it - the timeout fallback guarantees the pass still lands (whichever fires first wins).
    requestAnimationFrame(run);
    setTimeout(run, 100);
  }

  private measureAll(): void {
    for (const el of this.root.querySelectorAll<HTMLElement>(SELECTOR)) {
      // A hovered label is mid-slide (text-indent shifts its scroll box) - skip it; its stamped
      // values are still valid, and the next mutation/resize pass will catch any real change.
      if (el.matches(':hover')) continue;

      const distance = el.scrollWidth - el.clientWidth;
      if (distance > 1) {
        el.setAttribute('data-truncated', '');
        el.style.setProperty('--bz-marquee-distance', `${distance}px`);
        el.style.setProperty(
          '--bz-marquee-duration',
          `${Math.min(MAX_MS, Math.max(MIN_MS, distance * MS_PER_PX))}ms`,
        );
      } else if (el.hasAttribute('data-truncated')) {
        el.removeAttribute('data-truncated');
        el.style.removeProperty('--bz-marquee-distance');
        el.style.removeProperty('--bz-marquee-duration');
      }
    }
  }

  dispose = (): void => {
    this.pending = false;
    this.resizeObserver?.disconnect();
    this.mutationObserver?.disconnect();
  };
}

/** Watch a root's `[data-bz-marquee]` descendants and stamp their overflow state. */
export const createMarquee = (root: HTMLElement): Marquee => new Marquee(root);
