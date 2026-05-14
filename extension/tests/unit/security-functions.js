
const severityMeta = {
  Pass:    { cls: "severity-pass",    icon: "✓" },
  Info:    { cls: "severity-info",    icon: "i" },
  Warning: { cls: "severity-warning", icon: "!" }
};

const severityMap = { 0: "Pass", 1: "Info", 2: "Warning" };

function resolveSeverity(severity) {
  if (typeof severity === "number") return severityMap[severity] || "Info";
  return severity;
}

function getSeverityMeta(sevKey) {
  return severityMeta[sevKey] || severityMeta.Info;
}

module.exports = { severityMeta, severityMap, resolveSeverity, getSeverityMeta };
