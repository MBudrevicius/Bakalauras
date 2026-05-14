
function classifyAiHighlight(score) {
  if (score < 40) return null; // no highlight
  if (score >= 80) return { color: "rgba(231, 76, 60, 0.25)", label: "very likely AI" };
  if (score >= 65) return { color: "rgba(230, 126, 34, 0.25)", label: "likely AI" };
  if (score >= 50) return { color: "rgba(241, 196, 15, 0.20)", label: "possibly AI" };
  return { color: "rgba(241, 196, 15, 0.10)", label: "slight signal" };
}

function classifyCredibilityHighlight(aiScore) {
  const credibility = Math.max(0, Math.min(100, 100 - aiScore));
  if (credibility >= 70) return null; // no highlight

  if (credibility <= 25) {
    return { credibility, color: "rgba(231, 76, 60, 0.28)", level: "very-low" };
  }
  if (credibility <= 45) {
    return { credibility, color: "rgba(230, 126, 34, 0.24)", level: "low" };
  }
  return { credibility, color: "rgba(241, 196, 15, 0.2)", level: "medium-low" };
}

module.exports = { classifyAiHighlight, classifyCredibilityHighlight };
