export function initializeTerminal(root, input, dotnet, autoFocus) {
  let stickToBottom = true;
  const scrollToBottom = () => { if (stickToBottom) root.scrollTop = root.scrollHeight; };
  const updateStickiness = () => { stickToBottom = root.scrollHeight - root.scrollTop - root.clientHeight < 32; };
  const focus = () => { if (!input.disabled) input.focus({ preventScroll: true }); };
  const setInput = (value, selectionStart) => {
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    const caret = selectionStart ?? value.length;
    input.setSelectionRange(caret, caret);
  };
  const onPointerDown = event => {
    if (event.target === input) return;
    requestAnimationFrame(() => { const selection = window.getSelection(); if (!selection || selection.isCollapsed) focus(); });
  };
  const onKeyDown = async event => {
    const key = event.key;
    const control = event.ctrlKey || event.metaKey;
    if (control && key.toLowerCase() === 'k') {
      event.preventDefault();
      setInput(input.value.slice(0, input.selectionStart), input.selectionStart);
      return;
    }
    if (control && key.toLowerCase() === 'w') {
      event.preventDefault();
      const start = input.selectionStart;
      const before = input.value.slice(0, start).replace(/\s*\S+\s*$/, '');
      setInput(before + input.value.slice(input.selectionEnd), before.length);
      return;
    }
    const handled = key === 'Enter' || key === 'ArrowUp' || key === 'ArrowDown' || key === 'Tab' || key === 'Escape' ||
      (control && ['l', 'u'].includes(key.toLowerCase()));
    if (!handled) return;
    event.preventDefault();
    await dotnet.invokeMethodAsync('HandleTerminalKey', key.length === 1 ? key.toLowerCase() : key, control, event.shiftKey);
    requestAnimationFrame(() => { focus(); input.setSelectionRange(input.value.length, input.value.length); scrollToBottom(); });
  };
  const onCopy = async event => {
    const pageSelection = window.getSelection();
    if (input.selectionStart !== input.selectionEnd || (pageSelection && !pageSelection.isCollapsed)) return;
    event.preventDefault();
    await dotnet.invokeMethodAsync('HandleTerminalKey', 'c', true, false);
    requestAnimationFrame(() => { focus(); scrollToBottom(); });
  };
  root.addEventListener('scroll', updateStickiness, { passive: true });
  root.addEventListener('pointerdown', onPointerDown);
  input.addEventListener('keydown', onKeyDown);
  input.addEventListener('copy', onCopy);
  const observer = new MutationObserver(() => requestAnimationFrame(scrollToBottom));
  observer.observe(root, { childList: true, subtree: true, characterData: true });
  const reportSize = () => {
    const style = getComputedStyle(root);
    const probe = document.createElement('span');
    probe.textContent = 'M'; probe.style.cssText = 'position:absolute;visibility:hidden;font:inherit';
    root.appendChild(probe);
    const cell = probe.getBoundingClientRect(); probe.remove();
    const columns = Math.max(1, Math.floor(root.clientWidth / Math.max(1, cell.width)));
    const rows = Math.max(1, Math.floor(root.clientHeight / Math.max(1, cell.height)));
    dotnet.invokeMethodAsync('HandleTerminalResize', columns, rows);
  };
  const resizeObserver = new ResizeObserver(() => { scrollToBottom(); reportSize(); });
  resizeObserver.observe(root);
  reportSize();
  if (autoFocus) requestAnimationFrame(() => { focus(); scrollToBottom(); });
  return { dispose() {
    observer.disconnect(); resizeObserver.disconnect();
    root.removeEventListener('scroll', updateStickiness);
    root.removeEventListener('pointerdown', onPointerDown);
    input.removeEventListener('keydown', onKeyDown);
    input.removeEventListener('copy', onCopy);
  }};
}

export function loadHistory(key) {
  try { const value = JSON.parse(localStorage.getItem(`corevar.cli.history.${key}`) || '[]'); return Array.isArray(value) ? value : []; }
  catch { return []; }
}

export function saveHistory(key, values) {
  localStorage.setItem(`corevar.cli.history.${key}`, JSON.stringify(values));
}

export function focusTerminal(input) {
  if (document.activeElement !== input && !input.disabled) input.focus({ preventScroll: true });
}
