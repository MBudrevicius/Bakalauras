import { fetchApi } from "./api.js";
import { showStatus, esc, aiScoreClass, aiBarColor, fetchStoredScores, showLoading, hideLoading, showErrorModal } from "./helpers.js";
import { confirmApiCost, shouldConfirmCost, estimateAiScanCost } from "./cost.js";
import { saveToHistory } from "./history.js";

export async function initAi(getWebTab, getCurrentUrl) {
  const aiFullBtn      = document.getElementById("ai-full-page-btn");
  const aiSelBtn       = document.getElementById("ai-selection-btn");
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

  // Restore saved state
  const { aiState } = await chrome.storage.session.get("aiState");
  if (aiState?.url === getCurrentUrl() && aiState.data) {
    if (aiState.isAllModels) {
      renderAllModelsResults(aiState.data);
    } else {
      renderAiResults(aiState.data);
    }
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

    if (modelSelect.value === "all-models" && !savedKey) {
      showStatus(aiStatus, "API key is required for All Models mode.", true);
      return;
    }

    if (savedKey && await shouldConfirmCost()) {
      const textLen = body.text?.length || 0;
      const est = estimateAiScanCost(textLen, modelSelect.value, null);
      const confirmed = await confirmApiCost(est);
      if (!confirmed) return;
    }

    aiFullBtn.disabled = true;
    aiSelBtn.disabled = true;
    aiResultsList.innerHTML = "";
    aiScoreSection.classList.add("hidden");
    showStatus(aiStatus, "");
    showLoading("Analyzing content…");

    try {
      const headers = {};
      if (savedKey) {
        headers["X-Claude-Api-Key"] = savedKey;
        headers["X-Claude-Model"] = modelSelect.value;
      }

      const isAllModels = modelSelect.value === "all-models";
      const endpoint = isAllModels ? "/api/ai-checks/all-models" : "/api/ai-checks";

      const data = await fetchApi(endpoint, {
        method: "POST",
        headers,
        body: JSON.stringify(body)
      });

      if (isAllModels) {
        renderAllModelsResults(data);
      } else {
        renderAiResults(data);
      }
      chrome.storage.session.set({ aiState: { url: getCurrentUrl(), data, isAllModels } });

      const score = isAllModels ? data.averageAiScore : (data.overallAiScore ?? 0);
      const results = isAllModels
        ? data.modelResults.map(r => ({ title: r.label, aiScore: r.overallAiScore, description: `Claude ${r.label} detection score` }))
        : data.results.map(r => ({ title: r.title, aiScore: r.aiScore, description: r.description }));

      await saveToHistory({
        type: "ai",
        url: getCurrentUrl(),
        score,
        results
      });
      // Refetch stored average from server (running average now updated)
      await fetchStoredScores(getCurrentUrl(), true);
      showStatus(aiStatus, data.textLength ? `Analyzed ${data.textLength} characters.` : "");
    } catch (err) {
      showErrorModal(err.message || "Failed to reach server.");
    } finally {
      hideLoading();
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

  function renderAllModelsResults(data) {
    const score = data.averageAiScore ?? 0;
    aiScoreValue.textContent = score + "%";
    aiScoreCircle.className = "score-circle " + aiScoreClass(score);
    aiScoreSection.classList.remove("hidden");

    aiResultsList.innerHTML = "";

    // Claude combined result (clickable to expand per-model breakdown)
    const avgClaudeScore = Math.round(data.modelResults.reduce((s, m) => s + m.aiScore, 0) / data.modelResults.length);
    const claudeLi = document.createElement("li");
    claudeLi.className = "result-item result-item-expandable";
    const claudeBarColor = aiBarColor(avgClaudeScore);
    claudeLi.innerHTML = `
      <div class="result-body" style="width:100%">
        <div style="display:flex; justify-content:space-between; align-items:center">
          <span class="result-title">Claude AI Detection <span class="expand-hint">▸</span></span>
          <span class="result-title" style="color:${claudeBarColor}">${avgClaudeScore}%</span>
        </div>
        <span class="result-desc">Average across all models. Click to see breakdown.</span>
        <div class="ai-bar"><div class="ai-bar-fill" style="width:${avgClaudeScore}%; background:${claudeBarColor}"></div></div>
        <div class="model-breakdown hidden">
          ${data.modelResults.map(m => {
            const mBarColor = aiBarColor(m.aiScore);
            return `<div class="model-breakdown-item">
              <div style="display:flex; justify-content:space-between; align-items:center">
                <span class="result-desc" style="color:#d0d4e0">${esc(m.label)}</span>
                <span class="result-desc" style="color:${mBarColor}">${m.aiScore}%</span>
              </div>
              <div class="ai-bar"><div class="ai-bar-fill" style="width:${m.aiScore}%; background:${mBarColor}"></div></div>
            </div>`;
          }).join("")}
        </div>
      </div>`;
    claudeLi.addEventListener("click", () => {
      const breakdown = claudeLi.querySelector(".model-breakdown");
      const hint = claudeLi.querySelector(".expand-hint");
      breakdown.classList.toggle("hidden");
      hint.textContent = breakdown.classList.contains("hidden") ? "▸" : "▾";
    });
    aiResultsList.appendChild(claudeLi);

    // Heuristic results (same as single-model view)
    for (const r of data.heuristicResults) {
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

}
