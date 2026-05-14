
export function extractPageSegments() {
  const CONTENT_TAGS = new Set(["P", "LI", "TD", "TH", "BLOCKQUOTE", "FIGCAPTION", "DD", "DT", "PRE"]);
  const BLOCK_TAGS = new Set(["P", "LI", "TD", "TH", "BLOCKQUOTE", "FIGCAPTION", "DD", "DT", "PRE", "DIV", "SECTION", "ARTICLE"]);
  const SKIP_TAGS = new Set([
    "NAV", "HEADER", "FOOTER", "ASIDE", "FORM", "BUTTON",
    "INPUT", "SELECT", "TEXTAREA", "SCRIPT", "STYLE", "NOSCRIPT",
    "SVG", "IFRAME", "VIDEO", "AUDIO", "CANVAS", "IMG"
  ]);
  const SKIP_ROLES = new Set(["navigation", "banner", "contentinfo", "complementary", "search"]);

  const contentRoot =
    document.querySelector("article") ||
    document.querySelector("main") ||
    document.querySelector("[role='main']") ||
    document.body;

  const segments = [];

  function shouldSkip(el) {
    if (SKIP_TAGS.has(el.tagName)) return true;
    const role = el.getAttribute("role");
    return role && SKIP_ROLES.has(role);
  }

  function walk(el) {
    if (shouldSkip(el)) return;

    if (CONTENT_TAGS.has(el.tagName)) {
      const text = (el.innerText || "").trim();
      if (text.length > 40) {
        el.setAttribute("data-ai-seg", segments.length.toString());
        segments.push({ text });
      }
      return;
    }

    for (const child of el.children) {
      walk(child);
    }
  }

  walk(contentRoot);

  if (segments.length === 0) {
    function isLeafTextDiv(el) {
      if (el.tagName !== "DIV" && el.tagName !== "SPAN") return false;
      for (const child of el.children) {
        if (BLOCK_TAGS.has(child.tagName)) return false;
      }
      return true;
    }

    function walkFallback(el) {
      if (shouldSkip(el)) return;

      if (isLeafTextDiv(el)) {
        const text = (el.innerText || "").trim();
        if (text.length > 40) {
          el.setAttribute("data-ai-seg", segments.length.toString());
          segments.push({ text });
        }
        return;
      }

      for (const child of el.children) {
        walkFallback(child);
      }
    }

    walkFallback(contentRoot);
  }

  return segments;
}

export function applyPerElementHighlight(scores) {
  document.querySelectorAll("[data-ai-checker-hl]").forEach(el => {
    el.style.removeProperty("background-color");
    el.removeAttribute("data-ai-checker-hl");
    el.removeAttribute("title");
  });

  for (let i = 0; i < scores.length; i++) {
    const score = scores[i];
    if (score < 40) continue;

    const el = document.querySelector(`[data-ai-seg="${i}"]`);
    if (!el) continue;

    let color;
    if (score >= 80)      color = "rgba(231, 76, 60, 0.25)";   // red - very likely AI
    else if (score >= 65) color = "rgba(230, 126, 34, 0.25)";  // orange - likely AI
    else if (score >= 50) color = "rgba(241, 196, 15, 0.20)";  // yellow - possibly AI
    else                  color = "rgba(241, 196, 15, 0.10)";  // faint yellow - slight signal

    el.style.backgroundColor = color;
    el.setAttribute("data-ai-checker-hl", "1");
    el.title = `AI probability: ${score}%`;
  }
}

export function applyCredibilityHighlight(scores, explanations, topic, relatedPages) {
  document.querySelectorAll("[data-ai-checker-hl]").forEach(el => {
    el.style.removeProperty("background-color");
    el.removeAttribute("data-ai-checker-hl");
    el.removeAttribute("title");
  });

  const sourceHints = Array.isArray(relatedPages)
    ? relatedPages
      .slice(0, 3)
      .map(p => {
        try {
          return new URL(p.url).hostname;
        } catch {
          return p.title || p.url || "Unknown source";
        }
      })
    : [];

  for (let i = 0; i < scores.length; i++) {
    const credibility = Math.max(0, Math.min(100, Number(scores[i] || 75)));
    if (credibility >= 60) continue;

    const el = document.querySelector(`[data-ai-seg="${i}"]`);
    if (!el) continue;

    let color;
    let severityLabel;
    if (credibility <= 20) {
      color = "rgba(231, 76, 60, 0.28)";
      severityLabel = "Very low credibility — likely misinformation.";
    } else if (credibility <= 40) {
      color = "rgba(230, 126, 34, 0.24)";
      severityLabel = "Low credibility — mostly inaccurate or misleading.";
    } else {
      color = "rgba(241, 196, 15, 0.2)";
      severityLabel = "Questionable credibility — verify with independent sources.";
    }

    const explanation = (Array.isArray(explanations) && explanations[i]) ? explanations[i] : "";
    const topicLine = topic ? `Topic: ${topic}` : "";
    const sourceLine = sourceHints.length > 0
      ? `Sources checked: ${sourceHints.join(", ")}`
      : "";

    el.style.backgroundColor = color;
    el.setAttribute("data-ai-checker-hl", "1");
    el.title = `Credibility: ${credibility}%. ${severityLabel}${explanation ? `\n${explanation}` : ""}${topicLine ? `\n${topicLine}` : ""}${sourceLine ? `\n${sourceLine}` : ""}`;
  }
}

export function clearHighlight() {
  document.querySelectorAll("[data-ai-checker-hl]").forEach(el => {
    el.style.removeProperty("background-color");
    el.removeAttribute("data-ai-checker-hl");
    el.removeAttribute("title");
  });
  document.querySelectorAll("[data-ai-seg]").forEach(el => {
    el.removeAttribute("data-ai-seg");
  });
}
