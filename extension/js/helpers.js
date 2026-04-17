// Severity metadata
export const severityMeta = {
  Pass:    { cls: "severity-pass",    icon: "✓" },
  Info:    { cls: "severity-info",    icon: "i" },
  Warning: { cls: "severity-warning", icon: "!" }
};
export const severityMap = { 0: "Pass", 1: "Info", 2: "Warning" };

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
export function updateTopScores({ securityScore, aiScore } = {}) {
  const section  = document.getElementById("stored-scores-section");
  const secEl    = document.getElementById("stored-sec-score");
  const aiEl     = document.getElementById("stored-ai-score");

  if (securityScore != null) {
    secEl.textContent = securityScore;
    secEl.className = "mini-value " + miniScoreClass(securityScore, false);
  }
  if (aiScore != null) {
    aiEl.textContent = aiScore + "%";
    aiEl.className = "mini-value " + miniScoreClass(aiScore, true);
  }

  section.classList.remove("hidden");
}

// Fetch stored scores from server
export async function fetchStoredScores(url) {
  const section = document.getElementById("stored-scores-section");
  const loading = document.getElementById("scores-loading");
  const content = document.getElementById("scores-content");

  section.classList.remove("hidden");
  loading.classList.remove("hidden");
  content.classList.add("hidden");

  try {
    const { fetchApi } = await import("./api.js");
    const data = await fetchApi(`/api/page-score?url=${encodeURIComponent(url)}`, { method: "GET" });
    if (data.found && data.score) {
      updateTopScores({ securityScore: data.score.securityScore, aiScore: data.score.aiScore });
      const meta = document.getElementById("score-meta");
      if (meta) {
        const date = new Date(data.score.lastChecked);
        const domain = data.score.domain || new URL(url).hostname;
        meta.textContent = `${domain} · ${date.toLocaleDateString()} · ${data.score.checkCount} check(s)`;
      }
      loading.classList.add("hidden");
      content.classList.remove("hidden");
    } else {
      section.classList.add("hidden");
    }
  } catch {
    section.classList.add("hidden");
  }
}
