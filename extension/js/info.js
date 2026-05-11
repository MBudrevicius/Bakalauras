import { fetchApi } from "./api.js";
import { showStatus, esc, secScoreClass, fetchStoredScores, showLoading, hideLoading, showErrorModal } from "./helpers.js";
import { extractPageSegments, applyCredibilityHighlight, clearHighlight } from "./highlight.js";

import { confirmApiCost, shouldConfirmCost, estimateCrossCheckCost, estimateAiScanCost } from "./cost.js";

export async function initInfo(getWebTab, getCurrentUrl) {
  const crossCheckBtn      = document.getElementById("cross-check-btn");
  const infoScoreSection   = document.getElementById("info-score-section");
  const infoScoreCircle    = document.getElementById("info-score-circle");
  const infoScoreValue     = document.getElementById("info-score-value");
  const infoResultsList    = document.getElementById("info-results-list");
  const infoStatus         = document.getElementById("info-status");
  const highlightToggle    = document.getElementById("info-highlight-toggle");

  let lastCrossCheckData = null;

  // API key - synced with AI tab via shared storage
  const apiKeyInput  = document.getElementById("info-claude-api-key");
  const apiKeyToggle = document.getElementById("info-api-key-toggle");
  const useAiChk     = document.getElementById("info-use-ai-chk");

  const { claudeApiKey, useAiEnabled } = await chrome.storage.local.get(["claudeApiKey", "useAiEnabled"]);
  if (claudeApiKey) {
    apiKeyInput.value = claudeApiKey;
    useAiChk.checked = useAiEnabled !== false;
  } else {
    useAiChk.checked = false;
  }

  apiKeyInput.addEventListener("change", () => {
    const key = apiKeyInput.value.trim();
    chrome.storage.local.set({ claudeApiKey: key });
    if (key && !useAiChk.checked) {
      useAiChk.checked = true;
      chrome.storage.local.set({ useAiEnabled: true });
    } else if (!key) {
      useAiChk.checked = false;
      chrome.storage.local.set({ useAiEnabled: false });
    }
  });

  useAiChk.addEventListener("change", () => {
    chrome.storage.local.set({ useAiEnabled: useAiChk.checked });
  });

  chrome.storage.onChanged.addListener((changes) => {
    if (changes.claudeApiKey) {
      apiKeyInput.value = changes.claudeApiKey.newValue || "";
    }
    if (changes.useAiEnabled) {
      useAiChk.checked = !!changes.useAiEnabled.newValue;
    }
    if (changes.claudeModel) {
      modelSelect.value = changes.claudeModel.newValue || "claude-haiku-4-5-20251001";
    }
  });

  apiKeyToggle.addEventListener("click", () => {
    apiKeyInput.type = apiKeyInput.type === "password" ? "text" : "password";
  });

  // Model selector - synced with AI tab
  const modelSelect = document.getElementById("info-claude-model");
  const { claudeModel } = await chrome.storage.local.get("claudeModel");
  if (claudeModel) modelSelect.value = claudeModel;

  modelSelect.addEventListener("change", () => {
    chrome.storage.local.set({ claudeModel: modelSelect.value });
  });

  const { infoHighlightEnabled } = await chrome.storage.local.get("infoHighlightEnabled");
  highlightToggle.checked = !!infoHighlightEnabled;

  highlightToggle.addEventListener("change", async () => {
    chrome.storage.local.set({ infoHighlightEnabled: highlightToggle.checked });
    if (!highlightToggle.checked) {
      await removeHighlights();
    }
  });

  // Restore saved cross-check state
  chrome.storage.session.get("infoState", ({ infoState }) => {
    if (infoState?.url === getCurrentUrl() && infoState.data) {
      lastCrossCheckData = infoState.data;
      renderCrossCheckResults(infoState.data);
      if (highlightToggle.checked) {
        runCredibilityHighlight(infoState.data);
      }
    }
  });

  const infoSelBtn = document.getElementById("info-selection-btn");

  crossCheckBtn.addEventListener("click", async () => {
    const url = getCurrentUrl();
    if (!url) { showStatus(infoStatus, "No URL detected.", true); return; }

    try {
      const tab = await getWebTab();
      const [{ result: pageTitle }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => document.title
      });

      const [{ result: pageText }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => (document.querySelector('article') || document.querySelector('main') || document.body).innerText.substring(0, 5000)
      });

      const [{ result: pageLinks }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => [...document.querySelectorAll('a[href]')]
          .map(a => a.href)
          .filter(h => h.startsWith('http'))
          .slice(0, 50)
      });

      await runCrossCheck({ url, title: pageTitle || "", text: pageText || "", pageLinks: pageLinks || [] });
    } catch (err) {
      showStatus(infoStatus, "Could not extract page content: " + err.message, true);
    }
  });

  infoSelBtn.addEventListener("click", async () => {
    try {
      const tab = await getWebTab();
      const [{ result }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => window.getSelection()?.toString() || ""
      });

      if (!result || !result.trim()) {
        showStatus(infoStatus, "No text selected on the page.", true);
        return;
      }

      const [{ result: pageLinks }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: () => [...document.querySelectorAll('a[href]')]
          .map(a => a.href)
          .filter(h => h.startsWith('http'))
          .slice(0, 50)
      });

      await runCrossCheck({ url: getCurrentUrl(), title: "", text: result, pageLinks: pageLinks || [] });
    } catch (err) {
      showStatus(infoStatus, "Could not read selection: " + err.message, true);
    }
  });

  async function runCrossCheck({ url, title, text, pageLinks }) {
    crossCheckBtn.disabled = true;
    infoSelBtn.disabled = true;
    infoResultsList.innerHTML = "";
    infoScoreSection.classList.add("hidden");
    await removeHighlights();
    showStatus(infoStatus, "");

    try {
      const headers = { "Content-Type": "application/json" };
      const savedKey = useAiChk.checked ? apiKeyInput.value.trim() : "";

      const isAllModels = modelSelect.value === "all-models";

      if (isAllModels && !savedKey) {
        showStatus(infoStatus, "API key is required for All Models mode.", true);
        crossCheckBtn.disabled = false;
        infoSelBtn.disabled = false;
        return;
      }

      if (savedKey && await shouldConfirmCost()) {
        const textLen = (text || "").length;
        const est = estimateCrossCheckCost(textLen, modelSelect.value);
        if (highlightToggle.checked) {
          est.desc = (est.desc || "Cross-check") + " + paragraph highlighting";
          est.estimatedCost = (est.estimatedCost || 0) * 1.4;
        }
        const confirmed = await confirmApiCost(est);
        if (!confirmed) { crossCheckBtn.disabled = false; infoSelBtn.disabled = false; return; }
      }

      showLoading("Cross-checking information…");

      if (savedKey) {
        headers["X-Claude-Api-Key"] = savedKey;
        headers["X-Claude-Model"] = modelSelect.value;
      }

      const endpoint = isAllModels ? "/api/cross-check/all-models" : "/api/cross-check";
      const data = await fetchApi(endpoint, {
        method: "POST",
        headers,
        body: JSON.stringify({ url, title, text, pageLinks: pageLinks || [] })
      });

      lastCrossCheckData = data;
      renderCrossCheckResults(data);
      chrome.storage.session.set({ infoState: { url, data } });
      await fetchStoredScores(url, true);

      if (highlightToggle.checked) {
        await runCredibilityHighlight(data, true);
      } else {
        showStatus(infoStatus, "");
      }
    } catch (err) {
      showErrorModal(err.message || "Failed to reach server.");
    } finally {
      hideLoading();
      crossCheckBtn.disabled = false;
      infoSelBtn.disabled = false;
    }
  }

  function renderCrossCheckResults(data) {
    const credScore = data.credibility?.score;
    const pages = Array.isArray(data.relatedPages) ? data.relatedPages : [];
    const sourceReliability = Array.isArray(data.sourceReliability) ? data.sourceReliability : [];
    const pageLinkDomains = data.pageLinkDomains || 0;

    // Calculate sub-scores
    const reliableSourceCount = sourceReliability.filter(s => s.score >= 50).length;
    const coverageScore = reliableSourceCount >= 5 ? 100 : reliableSourceCount >= 3 ? 70 : reliableSourceCount > 0 ? 40 : 0;
    const diversityScore = pageLinkDomains >= 5 ? 100 : pageLinkDomains >= 3 ? 70 : pageLinkDomains > 0 ? 40 : 0;

    const overallScore = credScore != null
      ? Math.round(credScore * 0.6 + coverageScore * 0.2 + diversityScore * 0.2)
      : Math.round(coverageScore * 0.5 + diversityScore * 0.5);

    // Show score circle
    infoScoreValue.textContent = overallScore;
    infoScoreCircle.className = "score-circle " + secScoreClass(overallScore);
    infoScoreSection.classList.remove("hidden");

    // Build results list with bar UI
    infoResultsList.innerHTML = "";

    // 1. AI Credibility Assessment (FIRST - most important)
    const claims = data.credibility?.claims || [];
    const hasCredibility = credScore != null && (credScore > 0 || (data.credibility?.verdict && data.credibility.verdict !== "Unknown"));
    const aiDesc = !hasCredibility
      ? "Unavailable. Enable Use AI and provide a Claude API key."
      : `${data.credibility.verdict || "Unknown"} — ${claims.filter(c => c.toLowerCase().includes("supported")).length} supported, ${claims.filter(c => c.toLowerCase().includes("contradicted") || c.toLowerCase().includes("misleading")).length} contradicted/misleading.`;

    const modelResults = Array.isArray(data.modelResults) ? data.modelResults : [];
    const hasModelBreakdown = modelResults.length > 0;
    let modelBreakdownHtml = "";
    if (hasModelBreakdown) {
      modelBreakdownHtml = `<div class="model-breakdown" style="margin-top:8px;padding-top:8px;border-top:1px solid rgba(255,255,255,0.06)">` +
        modelResults.map(m => {
          const mScore = m.credibility?.score;
          const mColor = mScore != null ? infoBarColor(mScore) : "#6b7394";
          const mVerdict = m.credibility?.verdict || "N/A";
          return `<div style="margin-bottom:6px">
            <div style="display:flex;justify-content:space-between;align-items:center">
              <span class="result-desc" style="color:#d0d4e0">${esc(m.label)}</span>
              <span class="result-desc" style="color:${mColor}">${mScore != null ? mScore + '/100' : 'N/A'} — ${esc(mVerdict)}</span>
            </div>
            ${mScore != null ? `<div class="ai-bar"><div class="ai-bar-fill" style="width:${mScore}%;background:${mColor}"></div></div>` : ''}
          </div>`;
        }).join("") + `</div>`;
    }

    const credDetailHtml = renderClaimsHtml(claims) + modelBreakdownHtml;

    addCheckItemWithBar({
      title: hasModelBreakdown ? "AI Credibility Assessment (All Models)" : "AI Credibility Assessment",
      score: hasCredibility ? credScore : null,
      description: aiDesc,
      barColor: hasCredibility ? infoBarColor(credScore) : null,
      expandable: claims.length > 0 || hasModelBreakdown,
      detailHtml: credDetailHtml,
    });

    // 2. Source Coverage — with reliability scores
    const covSev = reliableSourceCount >= 5 ? "Pass" : reliableSourceCount >= 3 ? "Info" : "Warning";
    const covDesc = pages.length > 0
      ? `Found ${pages.length} sources, ${reliableSourceCount} verified as relevant.`
      : "No related pages were found for cross-checking.";
    addCheckItemWithBar({
      title: "Source Coverage & Reliability",
      score: coverageScore,
      description: covDesc,
      barColor: infoBarColor(coverageScore),
      expandable: pages.length > 0,
      detailHtml: renderRelatedPagesWithReliability(pages, sourceReliability),
    });

    // 3. Source Diversity (links IN the page)
    const divDesc = pageLinkDomains > 0
      ? `Article references ${pageLinkDomains} unique external domain(s).`
      : "No external source links found in the article.";
    addCheckItemWithBar({
      title: "Source Diversity (In-Page Links)",
      score: diversityScore,
      description: divDesc,
      barColor: infoBarColor(diversityScore),
      expandable: (data.pageLinkSamples || []).length > 0,
      detailHtml: renderPageLinksHtml(data.pageLinkSamples || []),
    });


  }

  function infoBarColor(s) {
    if (s >= 70) return "#2ecc71";
    if (s >= 45) return "#f1c40f";
    if (s >= 20) return "#e67e22";
    return "#e74c3c";
  }

  function addCheckItemWithBar({ title, score, description, barColor, expandable = false, detailHtml = "" }) {
    const li = document.createElement("li");
    li.className = "result-item" + (expandable ? " expandable" : "");

    const scoreDisplay = score != null ? `<span class="result-title" style="color:${barColor}">${score}/100</span>` : "";
    const barHtml = score != null
      ? `<div class="ai-bar"><div class="ai-bar-fill" style="width:${score}%; background:${barColor}"></div></div>`
      : "";

    let html = `
      <div class="result-body" style="width:100%">
        <div style="display:flex; justify-content:space-between; align-items:center">
          <span class="result-title">${esc(title)}</span>
          ${scoreDisplay}
        </div>
        <span class="result-desc">${esc(description)}</span>
        ${barHtml}
      </div>`;

    if (expandable) {
      html += `<span class="expand-icon">▶</span>`;
      html += `<div class="result-detail">${detailHtml}</div>`;
    }

    li.innerHTML = html;

    if (expandable) {
      li.addEventListener("click", (e) => {
        if (e.target.closest("a")) return;
        li.classList.toggle("expanded");
      });
    }

    infoResultsList.appendChild(li);
  }

  function renderRelatedPagesWithReliability(pages, reliability) {
    return pages.map((rp, idx) => {
      const rel = reliability[idx];
      const relScore = rel ? rel.score : null;
      const relColor = relScore != null ? infoBarColor(relScore) : "#6b7394";
      const relLabel = relScore != null ? `Relevance: ${relScore}/100` : "";
      return `
      <div class="related-item">
        <div style="display:flex; justify-content:space-between; align-items:center">
          <a class="related-title" href="${esc(rp.url)}" target="_blank" rel="noopener">${esc(rp.title || rp.url)}</a>
          ${relScore != null ? `<span style="font-size:10px;color:${relColor};font-weight:600">${relScore}</span>` : ""}
        </div>
        <span class="related-snippet">${esc(rp.snippet)}</span>
        ${relScore != null ? `<div class="ai-bar" style="margin-top:4px"><div class="ai-bar-fill" style="width:${relScore}%; background:${relColor}"></div></div>` : ""}
      </div>`;
    }).join("");
  }

  function renderPageLinksHtml(links) {
    if (links.length === 0) return "<span class='result-desc'>No external links detected.</span>";
    return links.map(link => {
      let domain = "";
      try { domain = new URL(link).hostname; } catch { domain = link; }
      return `<div class="related-item">
        <a class="related-title" href="${esc(link)}" target="_blank" rel="noopener">${esc(domain)}</a>
      </div>`;
    }).join("");
  }

  function renderClaimsHtml(claims) {
    return claims.map(claim => {
      const lower = claim.toLowerCase();
      let cls = "claim-unverifiable";
      if (lower.includes("supported")) cls = "claim-supported";
      else if (lower.includes("contradicted")) cls = "claim-contradicted";
      else if (lower.includes("misleading")) cls = "claim-contradicted";
      return `<div class="credibility-claim ${cls}">${esc(claim)}</div>`;
    }).join("");
  }

  async function runCredibilityHighlight(data, skipCostCheck = false) {
    try {
      const tab = await getWebTab();
      const [{ result: segments }] = await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: extractPageSegments
      });

      if (!segments || segments.length === 0) {
        showStatus(infoStatus, "No readable paragraphs found for credibility highlighting.", true);
        return;
      }

      const storedKey = useAiChk.checked ? apiKeyInput.value.trim() : "";

      if (!storedKey) {
        showStatus(infoStatus, "API key required for credibility highlighting.", true);
        highlightToggle.checked = false;
        chrome.storage.local.set({ infoHighlightEnabled: false });
        return;
      }

      if (!skipCostCheck && await shouldConfirmCost()) {
        const totalSegmentChars = segments.reduce((sum, s) => sum + Math.min(s.text.length, 300), 0);
        const est = estimateAiScanCost(0, modelSelect.value, { segmentCount: segments.length, totalSegmentChars });
        est.desc = `Credibility highlight analysis (${segments.length} paragraphs)`;
        const confirmed = await confirmApiCost(est);
        if (!confirmed) {
          highlightToggle.checked = false;
          chrome.storage.local.set({ infoHighlightEnabled: false });
          return;
        }
      }

      showStatus(infoStatus, `Analyzing ${segments.length} paragraphs for credibility…`);

      const headers = {
        "X-Claude-Api-Key": storedKey,
        "X-Claude-Model": modelSelect.value
      };

      // Build sources from cross-check data (reliable sources only)
      const reliability = Array.isArray(data.sourceReliability) ? data.sourceReliability : [];
      const relatedPages = Array.isArray(data.relatedPages) ? data.relatedPages : [];
      const sources = relatedPages
        .filter((_, idx) => !reliability[idx] || reliability[idx].score >= 50)
        .slice(0, 4)
        .map(rp => ({ title: rp.title, snippet: rp.snippet }));

      const highlight = await fetchApi("/api/cross-check/highlight", {
        method: "POST",
        headers,
        body: JSON.stringify({
          segments: segments.map(s => s.text),
          topic: data.topic || "",
          sources
        })
      });

      await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: applyCredibilityHighlight,
        args: [highlight.scores || [], highlight.explanations || [], data.topic || "", relatedPages]
      });

      const scores = highlight.scores || [];
      const highlightedCount = scores.filter(s => s < 60).length;
      showStatus(infoStatus, `Done. ${highlightedCount} of ${segments.length} paragraph(s) highlighted as low credibility. Hover to view details.`);
    } catch (err) {
      showStatus(infoStatus, "Could not apply credibility highlights: " + err.message, true);
    }
  }

  async function removeHighlights() {
    try {
      const tab = await getWebTab();
      await chrome.scripting.executeScript({
        target: { tabId: tab.id },
        func: clearHighlight
      });
    } catch {
      // Ignore if the target tab is inaccessible or closed.
    }
  }
}
