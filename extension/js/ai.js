import { fetchApi, API_BASE } from "./api.js";
import { showStatus, esc, aiScoreClass, aiBarColor, fetchStoredScores } from "./helpers.js";
import { confirmApiCost, shouldConfirmCost, estimateAiScanCost } from "./cost.js";
import { extractPageSegments, applyPerElementHighlight, clearHighlight } from "./highlight.js";
import { saveToHistory } from "./history.js";

export async function initAi(getWebTab, getCurrentUrl) {
  const aiFullBtn      = document.getElementById("ai-full-page-btn");
  const aiSelBtn       = document.getElementById("ai-selection-btn");
  const aiHighlightChk = document.getElementById("ai-highlight-toggle");
  const aiScoreSection = document.getElementById("ai-score-section");
  const aiScoreCircle  = document.getElementById("ai-score-circle");
  const aiScoreValue   = document.getElementById("ai-score-value");
  const aiResultsList  = document.getElementById("ai-results-list");
  const aiStatus       = document.getElementById("ai-status");

  // API key management
  const apiKeyInput  = document.getElementById("claude-api-key");
  const apiKeyToggle = document.getElementById("api-key-toggle");
  const useAiChk     = document.getElementById("use-ai-chk");

  const { claudeApiKey, useAiEnabled } = await chrome.storage.local.get(["claudeApiKey", "useAiEnabled"]);
  if (claudeApiKey) {
    apiKeyInput.value = claudeApiKey;
    useAiChk.checked = useAiEnabled !== false; // default to true when key exists
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

  // Model selector
  const modelSelect = document.getElementById("claude-model");
  const { claudeModel } = await chrome.storage.local.get("claudeModel");
  if (claudeModel) modelSelect.value = claudeModel;

  modelSelect.addEventListener("change", () => {
    chrome.storage.local.set({ claudeModel: modelSelect.value });
  });

  // Restore highlight toggle state
  const { aiHighlightEnabled } = await chrome.storage.local.get("aiHighlightEnabled");
  if (aiHighlightEnabled) aiHighlightChk.checked = true;

  // Restore saved state
  const { aiState } = await chrome.storage.session.get("aiState");
  if (aiState?.url === getCurrentUrl() && aiState.data) {
    renderAiResults(aiState.data);
  }

  // Full page scan
  aiFullBtn.addEventListener("click", async () => {
    const currentUrl = getCurrentUrl();
    if (!currentUrl) { showStatus(aiStatus, "No URL detected.", true); return; }

    showStatus(aiStatus, "Extracting page text…");
    try {
      const tab = await getWebTab();
      const [{ result }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => {
          let text = document.body.innerText || "";
          document.querySelectorAll("textarea, input[type='text'], [contenteditable='true']").forEach(el => {
            const val = el.value || el.innerText || "";
            if (val.trim().length > 20) text += "\n" + val;
          });
          return text;
        }
      });

      if (!result || !result.trim()) {
        showStatus(aiStatus, "Could not extract text from this page.", true);
        return;
      }
      await runAiCheck({ text: result, url: currentUrl });
    } catch {
      await runAiCheck({ url: currentUrl });
    }
  });

  // Selected text scan
  aiSelBtn.addEventListener("click", async () => {
    try {
      const tab = await getWebTab();
      const [{ result }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => window.getSelection()?.toString() || ""
      });

      if (!result || !result.trim()) {
        showStatus(aiStatus, "No text selected on the page.", true);
        return;
      }
      await runAiCheck({ text: result, url: getCurrentUrl() });
    } catch (err) {
      showStatus(aiStatus, "Could not read selection: " + err.message, true);
    }
  });

  async function runAiCheck(body) {
    const savedKey = useAiChk.checked ? apiKeyInput.value.trim() : "";

    if (savedKey && await shouldConfirmCost()) {
      const textLen = body.text?.length || 0;
      let highlight = null;
      let highlightFailed = false;

      if (aiHighlightChk.checked) {
        try {
          const tab = await getWebTab();
          const [{ result: segs }] = await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            func: extractPageSegments
          });
          if (segs && segs.length > 0) {
            const totalSegmentChars = segs.reduce((sum, s) => sum + Math.min(s.text.length, 300), 0);
            highlight = { segmentCount: segs.length, totalSegmentChars };
          } else {
            highlightFailed = true;
          }
        } catch {
          highlightFailed = true;
        }
      }

      const est = estimateAiScanCost(textLen, modelSelect.value, highlight);
      if (aiHighlightChk.checked && highlightFailed) {
        est.desc += " (highlight unavailable on this page)";
      }
      const confirmed = await confirmApiCost(est);
      if (!confirmed) return;
    }

    aiFullBtn.disabled = true;
    aiSelBtn.disabled = true;
    aiResultsList.innerHTML = "";
    aiScoreSection.classList.add("hidden");
    showStatus(aiStatus, "Analyzing content\u2026");

    try {
      const headers = {};
      if (savedKey) {
        headers["X-Claude-Api-Key"] = savedKey;
        headers["X-Claude-Model"] = modelSelect.value;
      }

      const data = await fetchApi("/api/ai-checks", {
        method: "POST",
        headers,
        body: JSON.stringify(body)
      });
      renderAiResults(data);
      chrome.storage.session.set({ aiState: { url: getCurrentUrl(), data } });
      await saveToHistory({
        type: "ai",
        url: getCurrentUrl(),
        score: data.overallAiScore ?? 0,
        results: data.results.map(r => ({ title: r.title, aiScore: r.aiScore, description: r.description }))
      });
      // Refetch stored average from server (running average now updated)
      await fetchStoredScores(getCurrentUrl());
      showStatus(aiStatus, data.textLength ? `Analyzed ${data.textLength} characters.` : "");

      if (aiHighlightChk.checked) {
        await runHighlightAnalysis(true);
      }
    } catch (err) {
      showStatus(aiStatus, err.message || "Failed to reach server.", true);
    } finally {
      aiFullBtn.disabled = false;
      aiSelBtn.disabled = false;
    }
  }

  function renderAiResults(data) {
    const score = data.overallAiScore ?? 0;
    aiScoreValue.textContent = score + "%";
    aiScoreCircle.className = "score-circle " + aiScoreClass(score);
    aiScoreSection.classList.remove("hidden");

    aiResultsList.innerHTML = "";
    for (const r of data.results) {
      const li = document.createElement("li");
      li.className = "result-item";
      const barColor = aiBarColor(r.aiScore);
      li.innerHTML = `
        <div class="result-body" style="width:100%">
          <div style="display:flex; justify-content:space-between; align-items:center">
            <span class="result-title">${esc(r.title)}</span>
            <span class="result-title" style="color:${barColor}">${r.aiScore}%</span>
          </div>
          <span class="result-desc">${esc(r.description)}</span>
          <div class="ai-bar"><div class="ai-bar-fill" style="width:${r.aiScore}%; background:${barColor}"></div></div>
        </div>`;
      aiResultsList.appendChild(li);
    }
  }

  // Highlight toggle
  aiHighlightChk.addEventListener("change", async () => {
    chrome.storage.local.set({ aiHighlightEnabled: aiHighlightChk.checked });
    if (!aiHighlightChk.checked) {
      await removeHighlights();
    }
  });

  async function runHighlightAnalysis(skipCostConfirm = false) {
    try {
      const tab = await getWebTab();

      const [{ result: segments }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: extractPageSegments
      });

      if (!segments || segments.length === 0) return;

      const storedKey = useAiChk.checked ? apiKeyInput.value.trim() : "";

      if (!skipCostConfirm && storedKey && await shouldConfirmCost()) {
        const totalSegmentChars = segments.reduce((sum, s) => sum + Math.min(s.text.length, 300), 0);
        const est = estimateAiScanCost(0, modelSelect.value, { segmentCount: segments.length, totalSegmentChars });
        est.desc = `Highlight analysis (${segments.length} paragraphs)`;
        const confirmed = await confirmApiCost(est);
        if (!confirmed) return;
      }

      showStatus(aiStatus, `Analyzing ${segments.length} paragraphs for highlighting\u2026`);

      const headers = { "Content-Type": "application/json" };
      if (storedKey) {
        headers["X-Claude-Api-Key"] = storedKey;
        headers["X-Claude-Model"] = modelSelect.value;
      }

      const res = await fetch(`${API_BASE}/api/ai-checks/highlight`, {
        method: "POST",
        headers,
        body: JSON.stringify({ segments: segments.map(s => s.text) })
      });
      if (!res.ok) {
        showStatus(aiStatus, "Highlight analysis failed.", true);
        return;
      }
      const data = await res.json();

      await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: applyPerElementHighlight,
        args: [data.scores]
      });

      const highlighted = data.scores.filter(s => s >= 50).length;
      showStatus(aiStatus, `Highlighted ${highlighted}/${segments.length} paragraphs with AI indicators.`);
    } catch (err) {
      showStatus(aiStatus, "Could not apply highlights: " + err.message, true);
    }
  }

  async function removeHighlights() {
    try {
      const tab = await getWebTab();
      await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: clearHighlight
      });
    } catch { /* ignore */ }
  }
}
