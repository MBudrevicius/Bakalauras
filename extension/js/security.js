import { fetchApi } from "./api.js";
import { showStatus, esc, secScoreClass, severityMeta, severityMap, updateTopScores, fetchStoredScores, showLoading, hideLoading, showErrorModal } from "./helpers.js";
import { saveToHistory } from "./history.js";

export function initSecurity(getWebTab, getCurrentUrl) {
  const runBtn       = document.getElementById("run-checks-btn");
  const scoreSection = document.getElementById("score-section");
  const scoreCircle  = document.getElementById("score-circle");
  const scoreValue   = document.getElementById("score-value");
  const resultsList  = document.getElementById("results-list");
  const secStatus    = document.getElementById("security-status");

  chrome.storage.session.get("securityState", ({ securityState }) => {
    if (securityState?.url === getCurrentUrl() && securityState.data) {
      renderSecurityResults(securityState.data);
    }
  });

  runBtn.addEventListener("click", async () => {
    const currentUrl = getCurrentUrl();
    if (!currentUrl) { showStatus(secStatus, "No URL detected.", true); return; }
    runBtn.disabled = true;
    resultsList.innerHTML = "";
    scoreSection.classList.add("hidden");
    showStatus(secStatus, "");
    showLoading("Running security checks…");

    try {
      const data = await fetchApi("/api/security-checks", {
        method: "POST",
        body: JSON.stringify({ url: currentUrl })
      });
      renderSecurityResults(data);
      chrome.storage.session.set({ securityState: { url: currentUrl, data } });
      await saveToHistory({
        type: "security",
        url: currentUrl,
        score: data.overallScore ?? 0,
        results: data.results.map(r => ({ title: r.title, severity: r.severity, description: r.description }))
      });
      await fetchStoredScores(currentUrl, true);
      showStatus(secStatus, "");
    } catch (err) {
      showErrorModal(err.message || "Failed to reach server.");
    } finally {
      hideLoading();
      runBtn.disabled = false;
    }
  });

  function renderSecurityResults(data) {
    const score = data.overallScore ?? 0;
    scoreValue.textContent = score;
    scoreCircle.className = "score-circle " + secScoreClass(score);
    scoreSection.classList.remove("hidden");

    resultsList.innerHTML = "";
    for (const r of data.results) {
      const sevKey = typeof r.severity === "number" ? (severityMap[r.severity] || "Info") : r.severity;
      const meta = severityMeta[sevKey] || severityMeta.Info;
      const li = document.createElement("li");
      li.className = "result-item";
      li.innerHTML = `
        <div class="severity-icon ${meta.cls}">${meta.icon}</div>
        <div class="result-body">
          <span class="result-title">${esc(r.title)}</span>
          <span class="result-desc">${esc(r.description)}</span>
        </div>`;
      resultsList.appendChild(li);
    }
  }
}
