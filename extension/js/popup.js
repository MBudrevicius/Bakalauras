import { initSecurity } from "./security.js";
import { initAi } from "./ai.js";
import { initInfo } from "./info.js";
import { fetchStoredScores } from "./helpers.js";

document.addEventListener("DOMContentLoaded", async () => {
  const disclaimerOverlay  = document.getElementById("disclaimer-overlay");
  const disclaimerAccept   = document.getElementById("disclaimer-accept-btn");
  const disclaimerRemember = document.getElementById("disclaimer-remember-chk");

  const { disclaimerAccepted } = await chrome.storage.local.get("disclaimerAccepted");
  const mainContainer = document.querySelector(".container");
  if (!disclaimerAccepted) {
    disclaimerOverlay.classList.remove("hidden");
    mainContainer.style.display = "none";
    await new Promise(resolve => {
      disclaimerAccept.addEventListener("click", async () => {
        if (disclaimerRemember.checked) {
          await chrome.storage.local.set({ disclaimerAccepted: true });
        }
        disclaimerOverlay.classList.add("hidden");
        mainContainer.style.display = "";
        resolve();
      });
    });
  }

  const params = new URLSearchParams(window.location.search);
  const isPoppedOut = params.has("tabId");
  let targetTabId = isPoppedOut ? parseInt(params.get("tabId"), 10) : null;

  if (isPoppedOut) document.body.classList.add("popped-out");

  document.getElementById("popout-btn").addEventListener("click", async () => {
    try {
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      const popupUrl = chrome.runtime.getURL(
        `popup.html?url=${encodeURIComponent(tab?.url || "")}&tabId=${tab?.id || 0}`
      );
      chrome.windows.create({ url: popupUrl, type: "popup", width: 420, height: 650 });
      window.close();
    } catch { /* ignore */ }
  });

  document.getElementById("history-btn").addEventListener("click", () => {
    const historyUrl = chrome.runtime.getURL("history.html");
    chrome.windows.create({ url: historyUrl, type: "popup", width: 420, height: 650 });
  });

  async function getWebTab() {
    if (targetTabId) {
      try { return await chrome.tabs.get(targetTabId); }
      catch { /* tab may have closed */ }
    }
    const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    return tab;
  }

  const urlDisplay = document.getElementById("url-display");
  let currentUrl = "";

  if (isPoppedOut) {
    currentUrl = params.get("url") || "";
    urlDisplay.textContent = currentUrl || "Unable to detect URL";
  } else {
    try {
      const tab = await getWebTab();
      currentUrl = tab?.url || "";
      targetTabId = tab?.id || null;
      urlDisplay.textContent = currentUrl || "Unable to detect URL";
    } catch {
      urlDisplay.textContent = "Unable to detect URL";
    }
  }

  const getCurrentUrl = () => currentUrl;

  const tabs      = document.querySelectorAll(".tab");
  const tabPanels = document.querySelectorAll(".tab-content");

  const { activeTab } = await chrome.storage.session.get("activeTab");
  if (activeTab) {
    tabs.forEach(t => t.classList.remove("active"));
    tabPanels.forEach(p => p.classList.remove("active"));
    const btn = document.querySelector(`.tab[data-tab="${activeTab}"]`);
    if (btn) {
      btn.classList.add("active");
      document.getElementById("tab-" + activeTab)?.classList.add("active");
    }
  }

  tabs.forEach(btn => {
    btn.addEventListener("click", () => {
      tabs.forEach(t => t.classList.remove("active"));
      tabPanels.forEach(p => p.classList.remove("active"));
      btn.classList.add("active");
      document.getElementById("tab-" + btn.dataset.tab).classList.add("active");
      chrome.storage.session.set({ activeTab: btn.dataset.tab });
    });
  });

  initSecurity(getWebTab, getCurrentUrl);
  await initAi(getWebTab, getCurrentUrl);
  await initInfo(getWebTab, getCurrentUrl);

  if (currentUrl) {
    fetchStoredScores(currentUrl);
  }
});
