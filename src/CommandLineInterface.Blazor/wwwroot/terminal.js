import { followTerminalOutput } from './terminal-scroll.js';

export function initializeTerminal(root, input, dotnet, autoFocus) {
  const output = followTerminalOutput(root);
  let disposed = false;
  const focus = () => { if (!input.disabled) input.focus({ preventScroll: true }); };
  const setInput = (value, selectionStart) => {
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    const caret = selectionStart ?? value.length;
    input.setSelectionRange(caret, caret);
  };
  const onClick = event => {
    if (event.target.closest('input, button, a, select, textarea')) return;
    requestAnimationFrame(() => { const selection = window.getSelection(); if (!disposed && (!selection || selection.isCollapsed)) focus(); });
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
    if (key === 'Enter') output.resume();
    await dotnet.invokeMethodAsync('HandleTerminalKey', key.length === 1 ? key.toLowerCase() : key, control, event.shiftKey);
    requestAnimationFrame(() => {
      if (disposed || document.activeElement !== input) return;
      input.setSelectionRange(input.value.length, input.value.length);
      output.schedule();
    });
  };
  const onCopy = async event => {
    const pageSelection = window.getSelection();
    if (input.selectionStart !== input.selectionEnd || (pageSelection && !pageSelection.isCollapsed)) return;
    event.preventDefault();
    await dotnet.invokeMethodAsync('HandleTerminalKey', 'c', true, false);
    output.schedule();
  };
  root.addEventListener('click', onClick);
  input.addEventListener('keydown', onKeyDown);
  input.addEventListener('copy', onCopy);
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
  const resizeObserver = new ResizeObserver(reportSize);
  resizeObserver.observe(root);
  reportSize();
  if (autoFocus) requestAnimationFrame(() => { if (!disposed) { focus(); output.schedule(); } });
  return { dispose() {
    disposed = true;
    output.dispose(); resizeObserver.disconnect();
    root.removeEventListener('click', onClick);
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
