// Injected into the web page via chrome.scripting.executeScript - must be self-contained

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

  // Fallback for SPAs (e.g. Twitter) that don't use standard content tags:
  // find leaf-level divs that contain substantial text but no nested block elements
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

// Apply per-element highlighting based on AI scores
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

// Clear all highlights and segment markers
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
