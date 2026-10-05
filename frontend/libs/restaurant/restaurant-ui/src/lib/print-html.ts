/**
 * Prints a document Printing rendered (TK-147), through the browser. The HTML
 * carries its own `@page` rule at the paper's exact size — an 80 mm roll for a
 * kitchen ticket — so it goes into a hidden frame and that frame is printed.
 */
export function printHtml(html: string): void {
  const frame = document.createElement('iframe');
  frame.setAttribute('aria-hidden', 'true');
  frame.style.position = 'fixed';
  frame.style.width = '0';
  frame.style.height = '0';
  frame.style.border = '0';
  frame.srcdoc = html;
  frame.onload = () => {
    frame.contentWindow?.focus();
    frame.contentWindow?.print();
    // Left in place a while: some browsers print asynchronously after print() returns.
    setTimeout(() => frame.remove(), 60_000);
  };
  document.body.appendChild(frame);
}
