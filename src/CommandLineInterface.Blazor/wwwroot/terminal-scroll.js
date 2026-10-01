// A portal may put the scrollbar on a wrapper instead of the terminal itself.
export function followTerminalOutput(root) {
  let scroller = root;
  for (let element = root; element; element = element.parentElement) {
    if (/^(auto|scroll|overlay)$/.test(getComputedStyle(element).overflowY)) {
      scroller = element;
      break;
    }
  }

  let following = true;
  let frame = 0;
  let disposed = false;
  let previousTop = scroller.scrollTop;
  let touchY;
  const atBottom = () => scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 4;
  const selectedOutput = () => {
    const selection = window.getSelection();
    return selection && !selection.isCollapsed &&
      (root.contains(selection.anchorNode) || root.contains(selection.focusNode));
  };
  const pause = () => { following = false; };
  const schedule = () => {
    if (disposed || frame || !following) return;
    frame = requestAnimationFrame(() => {
      frame = 0;
      if (disposed || !following || selectedOutput()) return;
      // Instant movement keeps pace with streamed output, even if the host uses smooth scrolling.
      scroller.scrollTo({ top: scroller.scrollHeight, behavior: 'instant' });
      previousTop = scroller.scrollTop;
    });
  };
  const resume = () => { following = true; schedule(); };
  const onScroll = () => {
    const top = scroller.scrollTop;
    // Resizing or trimming scrollback can lower scrollTop while still at the bottom.
    if (atBottom() && root.contains(document.activeElement) && !selectedOutput()) following = true;
    else if (top < previousTop - 1) pause();
    previousTop = top;
  };
  const onWheel = event => { if (event.deltaY < 0) pause(); };
  const onTouchStart = event => { touchY = event.touches[0]?.clientY; };
  const onTouchMove = event => {
    const next = event.touches[0]?.clientY;
    if (next > touchY) pause();
    touchY = next;
  };
  const onPointerDown = event => {
    // Clicking output, a scrollbar, another terminal control, or the page yields to the reader.
    if (!root.contains(event.target) || !event.target.closest?.('.terminal-input')) pause();
  };
  const onFocusOut = event => { if (!root.contains(event.relatedTarget)) pause(); };
  const onFocusIn = event => { if (!event.target.matches('.terminal-input')) pause(); };
  const onSelection = () => { if (selectedOutput()) pause(); };
  const onKeyDown = event => {
    if (event.key === 'PageUp' || (event.key === 'Home' && event.ctrlKey) ||
        (event.target.tagName !== 'INPUT' && ['ArrowUp', 'Home'].includes(event.key))) pause();
  };

  scroller.addEventListener('scroll', onScroll, { passive: true });
  scroller.addEventListener('wheel', onWheel, { passive: true });
  scroller.addEventListener('touchstart', onTouchStart, { passive: true });
  scroller.addEventListener('touchmove', onTouchMove, { passive: true });
  scroller.addEventListener('keydown', onKeyDown);
  root.addEventListener('focusout', onFocusOut);
  root.addEventListener('focusin', onFocusIn);
  document.addEventListener('pointerdown', onPointerDown, true);
  document.addEventListener('selectionchange', onSelection);
  window.addEventListener('blur', pause);
  const mutations = new MutationObserver(schedule);
  mutations.observe(root, { childList: true, subtree: true, characterData: true });
  const resize = new ResizeObserver(schedule);
  resize.observe(root);
  if (scroller !== root) resize.observe(scroller);
  schedule();

  return { resume, schedule, dispose() {
    disposed = true;
    cancelAnimationFrame(frame);
    mutations.disconnect(); resize.disconnect();
    scroller.removeEventListener('scroll', onScroll);
    scroller.removeEventListener('wheel', onWheel);
    scroller.removeEventListener('touchstart', onTouchStart);
    scroller.removeEventListener('touchmove', onTouchMove);
    scroller.removeEventListener('keydown', onKeyDown);
    root.removeEventListener('focusout', onFocusOut);
    root.removeEventListener('focusin', onFocusIn);
    document.removeEventListener('pointerdown', onPointerDown, true);
    document.removeEventListener('selectionchange', onSelection);
    window.removeEventListener('blur', pause);
  }};
}
