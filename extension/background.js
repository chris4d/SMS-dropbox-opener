// SMS DropboxOpener extension - background service worker.
// Intercepts navigations to the open.html bridge page and opens the target
// folder directly through the native messaging host, so the browser page
// never has to load. Without the extension, the bridge page itself renders
// as the two-click fallback, so the same link works everywhere.

const BRIDGE_PATTERN = 'https://chris4d.github.io/SMS-dropbox-opener/open.html?*';
const NATIVE_HOST = 'com.sms.dropboxopener';

chrome.webNavigation.onCommitted.addListener(async (details) => {
  if (details.frameId !== 0) return; // top frames only

  let url;
  try { url = new URL(details.url); } catch { return; }
  const p = url.searchParams.get('p');
  if (p === null) return; // no path to open; let the page explain itself

  // The nav cancellable only until committed; replacing is fine because the
  // native host does the real work and the page is only a fallback.
  chrome.tabs.update(details.tabId, { url: 'about:blank' });

  try {
    const resp = await chrome.runtime.sendNativeMessage(NATIVE_HOST, { path: p });
    if (resp && resp.ok === false) {
      // Unknown target: put the bridge page back so the user sees guidance.
      chrome.tabs.update(details.tabId, { url: details.url });
    }
  } catch (e) {
    // Native host missing/not registered: fall back to the bridge page.
    chrome.tabs.update(details.tabId, { url: details.url });
  }
}, { url: [{ urlPattern: BRIDGE_PATTERN }] });
