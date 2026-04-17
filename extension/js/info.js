import { fetchApi } from "./api.js";
import { showStatus, esc, miniScoreClass } from "./helpers.js";
import { confirmApiCost, shouldConfirmCost, estimateCrossCheckCost } from "./cost.js";

export async function initInfo(getWebTab, getCurrentUrl) {
  const crossCheckBtn       = document.getElementById("cross-check-btn");
  const crossTopic          = document.getElementById("cross-topic");
  const relatedList         = document.getElementById("related-list");
  const infoStatus          = document.getElementById("info-status");

  // API key - synced with AI tab via shared storage
  const apiKeyInput  = document.getElementById("info-claude-api-key");
  const apiKeyToggle = document.getElementById("info-api-key-toggle");
  const useAiChk     = document.getElementById("info-use-ai-chk");

  const { claudeApiKey, useAiEnabled } = await chrome.storage.local.get(["claudeApiKey", "useAiEnabled"]);
  if (claudeApiKey) {
    apiKeyInput.value = claudeApiKey;
    useAiChk.checked = useAiEnabled !== false;
  } else {
    useAiChk.checked = false;
  }

  apiKeyInput.addEventListener("change", () => {
    const key = apiKeyInput.value.trim();
    chrome.storage.local.set({ claudeApiKey: key });
    if (key && !useAiChk.checked) {
      useAiChk.checked = true;
      chrome.storage.local.set({ useAiEnabled: true });
    } else if (!key) {
      useAiChk.checked = false;
      chrome.storage.local.set({ useAiEnabled: false });
    }
  });

  useAiChk.addEventListener("change", () => {
    chrome.storage.local.set({ useAiEnabled: useAiChk.checked });
  });

  chrome.storage.onChanged.addListener((changes) => {
    if (changes.claudeApiKey) {
      apiKeyInput.value = changes.claudeApiKey.newValue || "";
    }
    if (changes.useAiEnabled) {
      useAiChk.checked = !!changes.useAiEnabled.newValue;
    }
    if (changes.claudeModel) {
      modelSelect.value = changes.claudeModel.newValue || "claude-haiku-4-5-20251001";
    }
  });

  apiKeyToggle.addEventListener("click", () => {
    apiKeyInput.type = apiKeyInput.type === "password" ? "text" : "password";
  });

  // Model selector - synced with AI tab
  const modelSelect = document.getElementById("info-claude-model");
  const { claudeModel } = await chrome.storage.local.get("claudeModel");
  if (claudeModel) modelSelect.value = claudeModel;

  modelSelect.addEventListener("change", () => {
    chrome.storage.local.set({ claudeModel: modelSelect.value });
  });

  // Restore saved cross-check state
  chrome.storage.session.get("infoState", ({ infoState }) => {
    if (infoState?.url === getCurrentUrl() && infoState.data) {
      renderCrossCheckResults(infoState.data);
    }
  });

  crossCheckBtn.addEventListener("click", async () => {
    const url = getCurrentUrl();
    if (!url) { showStatus(infoStatus, "No URL detected.", true); return; }
    crossCheckBtn.disabled = true;
    relatedList.innerHTML = "";
    crossTopic.classList.add("hidden");
    showStatus(infoStatus, "Cross-checking information…");

    try {
      const tab = await getWebTab();
      const [{ result: pageTitle }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => document.title
      });

      const [{ result: pageText }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => (document.querySelector('article') || document.querySelector('main') || document.body).innerText.substring(0, 5000)
      });

      const headers = { "Content-Type": "application/json" };
      const savedKey = useAiChk.checked ? apiKeyInput.value.trim() : "";

      if (savedKey && await shouldConfirmCost()) {
        const textLen = (pageText || "").length;
        const est = estimateCrossCheckCost(textLen, modelSelect.value);
        const confirmed = await confirmApiCost(est);
        if (!confirmed) { crossCheckBtn.disabled = false; return; }
      }

      if (savedKey) {
        headers["X-Claude-Api-Key"] = savedKey;
        headers["X-Claude-Model"] = modelSelect.value;
      }

      const data = await fetchApi("/api/cross-check", {
        method: "POST",
        headers,
        body: JSON.stringify({ url, title: pageTitle || "", text: pageText || "" })
      });
      renderCrossCheckResults(data);
      chrome.storage.session.set({ infoState: { url, data } });
      showStatus(infoStatus, "");
    } catch (err) {
      showStatus(infoStatus, err.message || "Failed to reach server.", true);
    } finally {
      crossCheckBtn.disabled = false;
    }
  });

  function renderCrossCheckResults(data) {
    if (data.topic) {
      crossTopic.innerHTML = `<strong>Topic:</strong> ${esc(data.topic)}`;
      crossTopic.classList.remove("hidden");
    }

    relatedList.innerHTML = "";
    if (data.relatedPages && data.relatedPages.length > 0) {
      for (const rp of data.relatedPages) {
        const li = document.createElement("li");
        li.className = "related-item";

        li.innerHTML = `
          <a class="related-title" href="${esc(rp.url)}" target="_blank" rel="noopener">${esc(rp.title || rp.url)}</a>
          <span class="related-snippet">${esc(rp.snippet)}</span>`;
        relatedList.appendChild(li);
      }
    } else {
      showStatus(infoStatus, data.topic
        ? "No related pages found. Make sure Brave Search API is configured."
        : "Could not determine page topic.");
    }
  }
}
