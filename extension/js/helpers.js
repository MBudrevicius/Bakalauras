// Severity metadata
export const severityMeta = {
  Pass:    { cls: "severity-pass",    icon: "✓" },
  Info:    { cls: "severity-info",    icon: "i" },
  Warning: { cls: "severity-warning", icon: "!" }
};
export const severityMap = { 0: "Pass", 1: "Info", 2: "Warning" };

// ═══ Loading Spinner ═══
export function showLoading(message = "Processing…") {
  const overlay = document.getElementById("loading-overlay");
  const text = document.getElementById("loading-text");
  if (overlay) {
    text.textContent = message;
    overlay.classList.remove("hidden");
  }
}

export function hideLoading() {
  const overlay = document.getElementById("loading-overlay");
  if (overlay) overlay.classList.add("hidden");
}

// ═══ Error Modal ═══
export function showErrorModal(message) {
  const overlay = document.getElementById("error-modal-overlay");
  const msgEl = document.getElementById("error-modal-message");
  const closeBtn = document.getElementById("error-modal-close-btn");
  if (!overlay) return;
  msgEl.textContent = message;
  overlay.classList.remove("hidden");
  const handler = () => {
    overlay.classList.add("hidden");
    closeBtn.removeEventListener("click", handler);
  };
  closeBtn.addEventListener("click", handler);
  overlay.addEventListener("click", (e) => {
    if (e.target === overlay) handler();
  }, { once: true });
}

// Score colour helpers
export function secScoreClass(s) {
  if (s >= 80) return "score-green";
  if (s >= 60) return "score-yellow";
  if (s >= 40) return "score-orange";
  return "score-red";
}

export function aiScoreClass(s) {
  if (s < 50) return "score-ai-low";
  if (s < 65) return "score-ai-medium";
  if (s < 80) return "score-ai-high";
  return "score-ai-vhigh";
}

export function aiBarColor(s) {
  if (s < 50) return "#27ae60";
  if (s < 65) return "#f1c40f";
  if (s < 80) return "#e67e22";
  return "#e74c3c";
}

// Mini-score colouring - isAi: higher = worse, !isAi: higher = better
export function miniScoreClass(s, isAi) {
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

export function showStatus(el, msg, isError = false) {
  el.textContent = msg;
  el.className = "status" + (isError ? " error" : "");
}

export function esc(str) {
  const d = document.createElement("div");
  d.textContent = str;
  return d.innerHTML;
}

// Update the page scores bar
export function updateTopScores({ securityScore, credibilityScore, aiScore, securityCheckCount, credibilityCheckCount, aiCheckCount } = {}) {
  const section  = document.getElementById("stored-scores-section");
  const secEl    = document.getElementById("stored-sec-score");
  const credEl   = document.getElementById("stored-cred-score");
  const aiEl     = document.getElementById("stored-ai-score");

  if (securityCheckCount === 0) {
    secEl.textContent = "N/A";
    secEl.className = "mini-value";
  } else if (securityScore != null) {
    secEl.textContent = securityScore;
    secEl.className = "mini-value " + miniScoreClass(securityScore, false);
  }

  if (credibilityCheckCount === 0) {
    credEl.textContent = "N/A";
    credEl.className = "mini-value";
  } else if (credibilityScore != null) {
    credEl.textContent = credibilityScore;
    credEl.className = "mini-value " + miniScoreClass(credibilityScore, false);
  }

  if (aiCheckCount === 0) {
    aiEl.textContent = "N/A";
    aiEl.className = "mini-value";
  } else if (aiScore != null) {
    aiEl.textContent = aiScore + "%";
    aiEl.className = "mini-value " + miniScoreClass(aiScore, true);
  }

  section.classList.remove("hidden");
}

// Fetch stored scores from server (with session cache)
export async function fetchStoredScores(url, forceRefresh = false) {
  const section = document.getElementById("stored-scores-section");
  const loading = document.getElementById("scores-loading");
  const content = document.getElementById("scores-content");

  // Try session cache first
  if (!forceRefresh) {
    const { cachedScores } = await chrome.storage.session.get("cachedScores");
    if (cachedScores && cachedScores.url === url && cachedScores.data) {
      applyScoreData(cachedScores.data, url);
      section.classList.remove("hidden");
      loading.classList.add("hidden");
      content.classList.remove("hidden");
      return;
    }
  }

  section.classList.remove("hidden");
  loading.classList.remove("hidden");
  content.classList.add("hidden");

  try {
    const { fetchApi } = await import("./api.js");
    const data = await fetchApi(`/api/page-score?url=${encodeURIComponent(url)}`, { method: "GET" });
    chrome.storage.session.set({ cachedScores: { url, data } });
    applyScoreData(data, url);
    loading.classList.add("hidden");
    content.classList.remove("hidden");
  } catch {
    updateTopScores({ securityCheckCount: 0, credibilityCheckCount: 0, aiCheckCount: 0 });
    loading.classList.add("hidden");
    content.classList.remove("hidden");
  }
}

function applyScoreData(data, url) {
  if (data.found && data.score) {
    updateTopScores({
      securityScore: data.score.securityScore,
      credibilityScore: data.score.credibilityScore,
      aiScore: data.score.aiScore,
      securityCheckCount: data.score.securityCheckCount,
      credibilityCheckCount: data.score.credibilityCheckCount,
      aiCheckCount: data.score.aiCheckCount,
    });
    const meta = document.getElementById("score-meta");
    if (meta) {
      const date = new Date(data.score.lastChecked);
      const domain = data.score.domain || new URL(url).hostname;
      meta.textContent = `${domain} · ${date.toLocaleDateString()} · ${data.score.checkCount} check(s)`;
    }
  } else {
    updateTopScores({ securityCheckCount: 0, credibilityCheckCount: 0, aiCheckCount: 0 });
  }
}
