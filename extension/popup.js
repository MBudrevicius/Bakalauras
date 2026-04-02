const API_BASE = "http://localhost:5101";

const severityMeta = {
  Pass:    { cls: "severity-pass",    icon: "✓" },
  Info:    { cls: "severity-info",    icon: "i" },
  Warning: { cls: "severity-warning", icon: "!" }
};
const severityMap = { 0: "Pass", 1: "Info", 2: "Warning" };

document.addEventListener("DOMContentLoaded", async () => {
  // ── Privacy Disclaimer ──
  const disclaimerOverlay = document.getElementById("disclaimer-overlay");
  const disclaimerAccept  = document.getElementById("disclaimer-accept-btn");
  const disclaimerRemember = document.getElementById("disclaimer-remember-chk");

  const { disclaimerAccepted } = await chrome.storage.local.get("disclaimerAccepted");
  if (!disclaimerAccepted) {
    disclaimerOverlay.classList.remove("hidden");
    await new Promise(resolve => {
      disclaimerAccept.addEventListener("click", async () => {
        if (disclaimerRemember.checked) {
          await chrome.storage.local.set({ disclaimerAccepted: true });
        }
        disclaimerOverlay.classList.add("hidden");
        resolve();
      });
    });
  }

  // ── Shared DOM ──
  const urlDisplay = document.getElementById("url-display");
  const tabs       = document.querySelectorAll(".tab");
  const tabPanels  = document.querySelectorAll(".tab-content");

  // ── Security DOM ──
  const runBtn       = document.getElementById("run-checks-btn");
  const scoreSection = document.getElementById("score-section");
  const scoreCircle  = document.getElementById("score-circle");
  const scoreValue   = document.getElementById("score-value");
  const resultsList  = document.getElementById("results-list");
  const secStatus    = document.getElementById("security-status");

  // ── AI DOM ──
  const aiFullBtn      = document.getElementById("ai-full-page-btn");
  const aiSelBtn       = document.getElementById("ai-selection-btn");
  const aiHighlightChk = document.getElementById("ai-highlight-toggle");
  const aiScoreSection = document.getElementById("ai-score-section");
  const aiScoreCircle  = document.getElementById("ai-score-circle");
  const aiScoreValue   = document.getElementById("ai-score-value");
  const aiResultsList  = document.getElementById("ai-results-list");
  const aiStatus       = document.getElementById("ai-status");

  // ── Info DOM ──
  const storedScoresSection = document.getElementById("stored-scores-section");
  const storedSecScore      = document.getElementById("stored-sec-score");
  const storedAiScore       = document.getElementById("stored-ai-score");
  const adjustedScoresDiv   = document.getElementById("adjusted-scores");
  const adjSecEl            = document.getElementById("adj-sec");
  const adjAiEl             = document.getElementById("adj-ai");
  const scoreMeta           = document.getElementById("score-meta");
  const crossCheckBtn       = document.getElementById("cross-check-btn");
  const crossTopic          = document.getElementById("cross-topic");
  const relatedList         = document.getElementById("related-list");
  const infoStatus          = document.getElementById("info-status");

  // ── Pop-out support ──
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
      window.close(); // close the small popup
    } catch { /* ignore */ }
  });

  // Helper: get the web-page tab (works in both popup and pop-out mode)
  async function getWebTab() {
    if (targetTabId) {
      try {
        const tab = await chrome.tabs.get(targetTabId);
        return tab;
      } catch { /* tab may have closed */ }
    }
    const [tab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    return tab;
  }

  // ── Current tab URL ──
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

  // ══════════════════════════════════════
  //  Tab switching
  // ══════════════════════════════════════
  tabs.forEach(btn => {
    btn.addEventListener("click", () => {
      tabs.forEach(t => t.classList.remove("active"));
      tabPanels.forEach(p => p.classList.remove("active"));
      btn.classList.add("active");
      document.getElementById("tab-" + btn.dataset.tab).classList.add("active");
    });
  });

  // ══════════════════════════════════════
  //  Security checks
  // ══════════════════════════════════════
  runBtn.addEventListener("click", async () => {
    if (!currentUrl) { showStatus(secStatus, "No URL detected.", true); return; }
    runBtn.disabled = true;
    resultsList.innerHTML = "";
    scoreSection.classList.add("hidden");
    showStatus(secStatus, "Running checks…");

    try {
      const res = await fetch(`${API_BASE}/api/security-checks`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url: currentUrl })
      });
      if (!res.ok) {
        const err = await res.json().catch(() => null);
        throw new Error(err?.error || `Server error ${res.status}`);
      }
      const data = await res.json();
      renderSecurityResults(data);
      showStatus(secStatus, "");
    } catch (err) {
      showStatus(secStatus, err.message || "Failed to reach server.", true);
    } finally {
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

  // ══════════════════════════════════════
  //  AI checks
  // ══════════════════════════════════════

  // Full page scan — extract text client-side for reliability
  aiFullBtn.addEventListener("click", async () => {
    if (!currentUrl) { showStatus(aiStatus, "No URL detected.", true); return; }

    showStatus(aiStatus, "Extracting page text…");
    try {
      const tab = await getWebTab();
      const [{ result }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => {
          // Remove script/style/noscript content, get visible text
          const clone = document.body.cloneNode(true);
          clone.querySelectorAll("script, style, noscript, svg, iframe").forEach(el => el.remove());
          return clone.innerText || clone.textContent || "";
        }
      });

      if (!result || !result.trim()) {
        showStatus(aiStatus, "Could not extract text from this page.", true);
        return;
      }
      await runAiCheck({ text: result });
    } catch (err) {
      // Fallback to server-side extraction
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
      await runAiCheck({ text: result });
    } catch (err) {
      showStatus(aiStatus, "Could not read selection: " + err.message, true);
    }
  });

  async function runAiCheck(body) {
    aiFullBtn.disabled = true;
    aiSelBtn.disabled = true;
    aiResultsList.innerHTML = "";
    aiScoreSection.classList.add("hidden");
    showStatus(aiStatus, "Analyzing content…");

    try {
      const res = await fetch(`${API_BASE}/api/ai-checks`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body)
      });
      if (!res.ok) {
        const err = await res.json().catch(() => null);
        throw new Error(err?.error || `Server error ${res.status}`);
      }
      const data = await res.json();
      renderAiResults(data);
      showStatus(aiStatus, data.textLength ? `Analyzed ${data.textLength} characters.` : "");

      // Send highlight info to content script if toggle is on
      if (aiHighlightChk.checked) {
        sendHighlightCommand(data.overallAiScore);
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
  aiHighlightChk.addEventListener("change", () => {
    if (!aiHighlightChk.checked) {
      removeHighlights();
    }
  });

  async function sendHighlightCommand(score) {
    try {
      const tab = await getWebTab();
      await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: applyHighlight,
        args: [score]
      });
    } catch { /* ignore if can't inject */ }
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

  // ══════════════════════════════════════
  //  Info / Cross-check
  // ══════════════════════════════════════

  // Load stored scores on popup open
  if (currentUrl) {
    fetchStoredScores(currentUrl);
  }

  async function fetchStoredScores(url) {
    try {
      const res = await fetch(`${API_BASE}/api/page-score?url=${encodeURIComponent(url)}`);
      if (!res.ok) return;
      const data = await res.json();
      if (data.found && data.score) {
        renderStoredScores(data.score);
      }
    } catch { /* silent */ }
  }

  function renderStoredScores(score) {
    storedSecScore.textContent = score.securityScore;
    storedSecScore.className = "mini-value " + miniScoreClass(score.securityScore, false);
    storedAiScore.textContent = score.aiScore + "%";
    storedAiScore.className = "mini-value " + miniScoreClass(score.aiScore, true);

    const date = new Date(score.lastChecked);
    scoreMeta.textContent = `Last checked: ${date.toLocaleDateString()} · ${score.checkCount} check(s)`;
    storedScoresSection.classList.remove("hidden");
  }

  crossCheckBtn.addEventListener("click", async () => {
    if (!currentUrl) { showStatus(infoStatus, "No URL detected.", true); return; }
    crossCheckBtn.disabled = true;
    relatedList.innerHTML = "";
    crossTopic.classList.add("hidden");
    adjustedScoresDiv.classList.add("hidden");
    showStatus(infoStatus, "Cross-checking information…");

    try {
      const res = await fetch(`${API_BASE}/api/cross-check`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url: currentUrl })
      });
      if (!res.ok) {
        const err = await res.json().catch(() => null);
        throw new Error(err?.error || `Server error ${res.status}`);
      }
      const data = await res.json();
      renderCrossCheckResults(data);
      showStatus(infoStatus, "");
    } catch (err) {
      showStatus(infoStatus, err.message || "Failed to reach server.", true);
    } finally {
      crossCheckBtn.disabled = false;
    }
  });

  function renderCrossCheckResults(data) {
    // Show topic
    if (data.topic) {
      crossTopic.innerHTML = `<strong>Topic:</strong> ${esc(data.topic)}`;
      crossTopic.classList.remove("hidden");
    }

    // Update stored scores if returned
    if (data.pageScore) {
      renderStoredScores(data.pageScore);
    }

    // Show adjusted scores
    if (data.adjustedSecurityScore != null && data.adjustedAiScore != null) {
      adjSecEl.textContent = `Security: ${data.adjustedSecurityScore}`;
      adjAiEl.textContent = `AI: ${data.adjustedAiScore}%`;
      adjustedScoresDiv.classList.remove("hidden");
    }

    // Related pages
    relatedList.innerHTML = "";
    if (data.relatedPages && data.relatedPages.length > 0) {
      for (const rp of data.relatedPages) {
        const li = document.createElement("li");
        li.className = "related-item";

        let scoresHtml = "";
        if (rp.securityScore != null || rp.aiScore != null) {
          const secPart = rp.securityScore != null
            ? `<span class="has-score">Sec: ${rp.securityScore}</span>` : `<span>Sec: —</span>`;
          const aiPart = rp.aiScore != null
            ? `<span class="has-score">AI: ${rp.aiScore}%</span>` : `<span>AI: —</span>`;
          scoresHtml = `<div class="related-scores">${secPart}${aiPart}</div>`;
        }

        li.innerHTML = `
          <a class="related-title" href="${esc(rp.url)}" target="_blank" rel="noopener">${esc(rp.title || rp.url)}</a>
          <span class="related-snippet">${esc(rp.snippet)}</span>
          ${scoresHtml}`;
        relatedList.appendChild(li);
      }
    } else {
      showStatus(infoStatus, data.topic
        ? "No related pages found. Make sure Google Custom Search API is configured."
        : "Could not determine page topic.");
    }
  }

  // ══════════════════════════════════════
  //  Helpers
  // ══════════════════════════════════════

  function secScoreClass(s) {
    if (s >= 80) return "score-green";
    if (s >= 60) return "score-yellow";
    if (s >= 40) return "score-orange";
    return "score-red";
  }
  function aiScoreClass(s) {
    if (s < 50) return "score-ai-low";
    if (s < 65) return "score-ai-medium";
    if (s < 80) return "score-ai-high";
    return "score-ai-vhigh";
  }
  function aiBarColor(s) {
    if (s < 50) return "#27ae60";
    if (s < 65) return "#f1c40f";
    if (s < 80) return "#e67e22";
    return "#e74c3c";
  }

  // For info tab mini-score colouring
  // isAi=true: higher = worse (more AI). isAi=false: higher = better (more secure).
  function miniScoreClass(s, isAi) {
    if (isAi) {
      if (s < 30) return "good";
      if (s < 55) return "ok";
      if (s < 75) return "bad";
      return "danger";
    }
    if (s >= 80) return "good";
    if (s >= 60) return "ok";
    if (s >= 40) return "bad";
    return "danger";
  }

  function showStatus(el, msg, isError = false) {
    el.textContent = msg;
    el.className = "status" + (isError ? " error" : "");
  }

  function esc(str) {
    const d = document.createElement("div");
    d.textContent = str;
    return d.innerHTML;
  }
});

// ══════════════════════════════════════
// Functions injected into the page
// ══════════════════════════════════════

function applyHighlight(score) {
  // Determine colour based on score
  let color;
  if (score >= 90)      color = "rgba(231, 76, 60, 0.25)";   // red
  else if (score >= 75) color = "rgba(230, 126, 34, 0.25)";  // orange
  else if (score >= 60) color = "rgba(241, 196, 15, 0.2)";   // yellow
  else if (score >= 50) color = "rgba(241, 196, 15, 0.35)";  // bright yellow
  else return; // below 50% — don't highlight

  // Remove previous highlights first
  document.querySelectorAll("[data-ai-checker-hl]").forEach(el => {
    el.style.removeProperty("background-color");
    el.removeAttribute("data-ai-checker-hl");
  });

  // Find the main content root — prefer <article> or <main>, fall back to <body>
  const contentRoot =
    document.querySelector("article") ||
    document.querySelector("main") ||
    document.querySelector("[role='main']") ||
    document.body;

  // Tags to skip entirely (navigation, UI chrome)
  const SKIP_TAGS = new Set([
    "NAV", "HEADER", "FOOTER", "ASIDE", "FORM", "BUTTON",
    "INPUT", "SELECT", "TEXTAREA", "SCRIPT", "STYLE", "NOSCRIPT",
    "SVG", "IFRAME", "VIDEO", "AUDIO", "CANVAS", "IMG"
  ]);
  // Skip elements with common non-content roles/classes
  const SKIP_ROLES = new Set(["navigation", "banner", "contentinfo", "complementary", "search"]);

  // Text-leaf tags we want to highlight
  const LEAF_TAGS = new Set(["P", "LI", "TD", "TH", "BLOCKQUOTE", "FIGCAPTION", "DD", "DT"]);

  function shouldSkip(el) {
    if (SKIP_TAGS.has(el.tagName)) return true;
    const role = el.getAttribute("role");
    if (role && SKIP_ROLES.has(role)) return true;
    return false;
  }

  function walk(el) {
    if (shouldSkip(el)) return;

    if (LEAF_TAGS.has(el.tagName)) {
      // Only highlight if it actually has meaningful text
      const text = (el.innerText || "").trim();
      if (text.length > 20) {
        el.style.backgroundColor = color;
        el.setAttribute("data-ai-checker-hl", "1");
      }
      return; // don't descend into children of a highlighted element
    }

    // Recurse into children
    for (const child of el.children) {
      walk(child);
    }
  }

  walk(contentRoot);
}

function clearHighlight() {
  document.querySelectorAll("[data-ai-checker-hl]").forEach(el => {
    el.style.removeProperty("background-color");
    el.removeAttribute("data-ai-checker-hl");
  });
}
