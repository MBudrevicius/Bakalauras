
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

const severityMap = { 0: "Pass", 1: "Info", 2: "Warning" };

module.exports = {
  secScoreClass,
  aiScoreClass,
  aiBarColor,
  miniScoreClass,
  severityMap,
};
