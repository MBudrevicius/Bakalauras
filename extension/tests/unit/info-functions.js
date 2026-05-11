// Pure functions extracted from info.js for testing

function computeCoverageScore(reliableSourceCount) {
  if (reliableSourceCount >= 5) return 100;
  if (reliableSourceCount >= 3) return 70;
  if (reliableSourceCount > 0) return 40;
  return 0;
}

function computeDiversityScore(pageLinkDomains) {
  if (pageLinkDomains >= 5) return 100;
  if (pageLinkDomains >= 3) return 70;
  if (pageLinkDomains > 0) return 40;
  return 0;
}

function computeOverallScore(credScore, coverageScore, diversityScore) {
  if (credScore != null) {
    return Math.round(credScore * 0.6 + coverageScore * 0.2 + diversityScore * 0.2);
  }
  return Math.round(coverageScore * 0.5 + diversityScore * 0.5);
}

function infoBarColor(s) {
  if (s >= 70) return "#2ecc71";
  if (s >= 45) return "#f1c40f";
  if (s >= 20) return "#e67e22";
  return "#e74c3c";
}

function classifyClaim(claim) {
  const lower = claim.toLowerCase();
  if (lower.includes("supported")) return "claim-supported";
  if (lower.includes("contradicted")) return "claim-contradicted";
  if (lower.includes("misleading")) return "claim-contradicted";
  return "claim-unverifiable";
}

function categorizeSeverity(reliableSourceCount) {
  if (reliableSourceCount >= 5) return "Pass";
  if (reliableSourceCount >= 3) return "Info";
  return "Warning";
}

module.exports = {
  computeCoverageScore,
  computeDiversityScore,
  computeOverallScore,
  infoBarColor,
  classifyClaim,
  categorizeSeverity,
};
