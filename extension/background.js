// SMS DropboxOpener extension - background service worker.
// Intercepts navigations to the open.html bridge page and opens the target
// folder directly through the native messaging host, so the browser page
// never has to load. Without the extension, the bridge page itself renders
// as the two-click fallback, so the same link works everywhere.

const BRIDGE_PREFIX = 'https://chris4d.github.io/SMS-dropbox-opener/open.html';
const NATIVE_HOST = 'com.sms.dropboxopener';

// Re-entry guard: when we restore the bridge page after a native-messaging
// failure, its commit fires this listener again. Cap attempts per tab.
const attempts = new Map();

chrome.webNavigation.onCommitted.addListener(async (details) => {
  if (details.frameId !== 0) return; // top frames only

  let url;
  try { url = new URL(details.url); } catch { return; }
  const p = url.searchParams.get('p');
  if (p === null) return; // no path to open; let the page explain itself

  const count = attempts.get(details.tabId) || 0;
  if (count >= 2) return; // already retried; leave the fallback page alone
  attempts.set(details.tabId, count + 1);
  if (attempts.size > 100) attempts.clear(); // service-worker memory hygiene

  // Blank the tab so the stale bridge UI never shows; the folder window is
  // the real result.
  chrome.tabs.update(details.tabId, { url: 'about:blank' });

  try {
    const resp = await chrome.runtime.sendNativeMessage(NATIVE_HOST, { path: p });
    if (resp && resp.ok === false) {
      // Unknown target: put the bridge page back so the user sees guidance.
      chrome.tabs.update(details.tabId, { url: details.url });
    }
    // success: folder opened; tab stays blank
  } catch (e) {
    // Native host missing/not registered: fall back to the bridge page.
    chrome.tabs.update(details.tabId, { url: details.url });
  }
}, { url: [{ urlPrefix: BRIDGE_PREFIX }] });
