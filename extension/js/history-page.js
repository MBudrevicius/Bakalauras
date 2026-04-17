import { getHistory, clearHistory } from "./history.js";
import { esc, secScoreClass, aiBarColor, severityMap, severityMeta } from "./helpers.js";

const historyList = document.getElementById("history-list");
const historyEmpty = document.getElementById("history-empty");
const filterType = document.getElementById("filter-type");
const clearBtn = document.getElementById("clear-btn");

function aiScoreClass(s) {
  if (s < 50) return "score-green";
  if (s < 65) return "score-yellow";
  if (s < 80) return "score-orange";
  return "score-red";
}

function formatTime(ts) {
  const d = new Date(ts);
  return d.toLocaleDateString() + " " + d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function renderSecurityEntry(entry) {
  const card = document.createElement("div");
  card.className = "history-card";

  const scoreClass = secScoreClass(entry.score);

  let resultsHtml = "";
  if (entry.results?.length) {
    const items = entry.results.map(r => {
      const sevKey = typeof r.severity === "number" ? (severityMap[r.severity] || "Info") : r.severity;
      const meta = severityMeta[sevKey] || severityMeta.Info;
      return `<li class="result-item">
        <div class="severity-icon ${meta.cls}">${meta.icon}</div>
        <div class="result-body">
          <span class="result-title">${esc(r.title)}</span>
          ${r.description ? `<span class="result-desc">${esc(r.description)}</span>` : ""}
        </div>
      </li>`;
    }).join("");
    resultsHtml = `<ul class="results-list">${items}</ul>`;
  }

  card.innerHTML = `
    <div class="history-entry-header">
      <div class="history-entry-left">
        <span class="badge badge-security">Security</span>
        <a class="history-url" href="${esc(entry.url)}" target="_blank" rel="noopener">${esc(entry.url)}</a>
      </div>
      <span class="history-time">${formatTime(entry.timestamp)}</span>
    </div>
    <div class="history-toggle">
      <div class="score-section">
        <div class="score-circle ${scoreClass}"><span>${entry.score}</span></div>
        <span class="score-label">Overall Score</span>
      </div>
      <span class="toggle-chevron">&#9662;</span>
    </div>
    <div class="history-details hidden">
      ${resultsHtml}
    </div>`;

  card.querySelector(".history-toggle").addEventListener("click", () => {
    card.classList.toggle("expanded");
    card.querySelector(".history-details").classList.toggle("hidden");
  });

  return card;
}

function renderAiEntry(entry) {
  const card = document.createElement("div");
  card.className = "history-card";

  const scoreClass = aiScoreClass(entry.score);

  let resultsHtml = "";
  if (entry.results?.length) {
    const items = entry.results.map(r => {
      const color = aiBarColor(r.aiScore);
      return `<li class="result-item">
        <div class="result-body" style="width:100%">
          <div style="display:flex; justify-content:space-between; align-items:center">
            <span class="result-title">${esc(r.title)}</span>
            <span class="result-title" style="color:${color}">${r.aiScore}%</span>
          </div>
          ${r.description ? `<span class="result-desc">${esc(r.description)}</span>` : ""}
          <div class="ai-bar"><div class="ai-bar-fill" style="width:${r.aiScore}%; background:${color}"></div></div>
        </div>
      </li>`;
    }).join("");
    resultsHtml = `<ul class="results-list">${items}</ul>`;
  }

  card.innerHTML = `
    <div class="history-entry-header">
      <div class="history-entry-left">
        <span class="badge badge-ai">AI Detection</span>
        <a class="history-url" href="${esc(entry.url)}" target="_blank" rel="noopener">${esc(entry.url)}</a>
      </div>
      <span class="history-time">${formatTime(entry.timestamp)}</span>
    </div>
    <div class="history-toggle">
      <div class="score-section">
        <div class="score-circle ${scoreClass}"><span>${entry.score}%</span></div>
        <span class="score-label">AI Probability</span>
      </div>
      <span class="toggle-chevron">&#9662;</span>
    </div>
    <div class="history-details hidden">
      ${resultsHtml}
    </div>`;

  card.querySelector(".history-toggle").addEventListener("click", () => {
    card.classList.toggle("expanded");
    card.querySelector(".history-details").classList.toggle("hidden");
  });

  return card;
}

async function render() {
  const history = await getHistory();
  const filter = filterType.value;
  const filtered = filter === "all" ? history : history.filter(h => h.type === filter);

  historyList.innerHTML = "";

  if (filtered.length === 0) {
    historyEmpty.classList.remove("hidden");
    return;
  }

  historyEmpty.classList.add("hidden");

  for (const entry of filtered) {
    const card = entry.type === "security"
      ? renderSecurityEntry(entry)
      : renderAiEntry(entry);
    historyList.appendChild(card);
  }
}

filterType.addEventListener("change", render);

clearBtn.addEventListener("click", async () => {
  if (confirm("Clear all check history?")) {
    await clearHistory();
    render();
  }
});

render();
