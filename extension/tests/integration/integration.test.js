/**
 * Integration tests for the browser extension - verifies cross-module interactions:
 * - Cost estimation pipeline (model selection → token calculation → cost formatting)
 * - Score computation pipeline (credibility + coverage + diversity → overall score → display class)
 * - Highlight classification pipeline (AI score → highlight class → credibility mapping)
 * - History management pipeline (entry creation → storage → trimming)
 * - Security result pipeline (severity resolution → metadata → score classification)
 */

const {
  secScoreClass,
  aiScoreClass,
  aiBarColor,
  miniScoreClass,
  severityMap,
} = require("../unit/helper-functions");

const {
  computeCoverageScore,
  computeDiversityScore,
  computeOverallScore,
  infoBarColor,
  classifyClaim,
  categorizeSeverity,
} = require("../unit/info-functions");

const { classifyAiHighlight, classifyCredibilityHighlight } = require("../unit/highlight-functions");

const { MAX_HISTORY, trimHistory, prependEntry } = require("../unit/history-functions");

const {
  getModelPricing,
  estimateAiScanCost,
  estimateCrossCheckCost,
  formatCost,
} = require("../unit/cost-functions");

const { severityMeta, resolveSeverity, getSeverityMeta } = require("../unit/security-functions");

// ============================================================================
// Integration: Full scoring pipeline (credibility → helper classification)
// ============================================================================

describe("Integration: Credibility score → display pipeline", () => {
  test("high credibility score flows through overall calculation and display correctly", () => {
    const credScore = 85;
    const reliableSources = 5;
    const pageLinkDomains = 6;

    // Step 1: Compute sub-scores
    const coverage = computeCoverageScore(reliableSources);
    const diversity = computeDiversityScore(pageLinkDomains);
    expect(coverage).toBe(100);
    expect(diversity).toBe(100);

    // Step 2: Compute overall score
    const overall = computeOverallScore(credScore, coverage, diversity);
    expect(overall).toBe(Math.round(85 * 0.6 + 100 * 0.2 + 100 * 0.2));
    expect(overall).toBe(91);

    // Step 3: Verify display color matches high score
    const color = infoBarColor(overall);
    expect(color).toBe("#2ecc71"); // green
  });

  test("low credibility with no sources produces warning display", () => {
    const credScore = 25;
    const reliableSources = 0;
    const pageLinkDomains = 0;

    const coverage = computeCoverageScore(reliableSources);
    const diversity = computeDiversityScore(pageLinkDomains);
    const overall = computeOverallScore(credScore, coverage, diversity);

    expect(overall).toBe(Math.round(25 * 0.6 + 0 * 0.2 + 0 * 0.2));
    expect(overall).toBe(15);

    const color = infoBarColor(overall);
    expect(color).toBe("#e74c3c"); // red (< 20)
  });

  test("no API key (null cred score) uses coverage+diversity only", () => {
    const credScore = null;
    const coverage = computeCoverageScore(3);
    const diversity = computeDiversityScore(4);

    const overall = computeOverallScore(credScore, coverage, diversity);
    expect(overall).toBe(Math.round(70 * 0.5 + 70 * 0.5));
    expect(overall).toBe(70);

    const color = infoBarColor(overall);
    expect(color).toBe("#2ecc71"); // green
  });

  test("source categorization aligns with coverage score", () => {
    // 5+ reliable sources = Pass severity AND 100 coverage
    expect(categorizeSeverity(5)).toBe("Pass");
    expect(computeCoverageScore(5)).toBe(100);

    // 3-4 sources = Info AND 70 coverage
    expect(categorizeSeverity(3)).toBe("Info");
    expect(computeCoverageScore(3)).toBe(70);

    // 0 sources = Warning AND 0 coverage
    expect(categorizeSeverity(0)).toBe("Warning");
    expect(computeCoverageScore(0)).toBe(0);
  });
});

// ============================================================================
// Integration: AI detection → highlight classification pipeline
// ============================================================================

describe("Integration: AI score → highlight → display pipeline", () => {
  test("high AI score produces correct highlight and display class", () => {
    const aiScore = 85;

    // Step 1: Highlight classification
    const highlight = classifyAiHighlight(aiScore);
    expect(highlight).not.toBeNull();
    expect(highlight.label).toBe("very likely AI");
    expect(highlight.color).toContain("231, 76, 60"); // red-ish

    // Step 2: Display class for score circle
    const scoreClass = aiScoreClass(aiScore);
    expect(scoreClass).toBe("score-ai-vhigh");

    // Step 3: Bar color
    const barColor = aiBarColor(aiScore);
    expect(barColor).toBe("#e74c3c"); // red
  });

  test("low AI score produces no highlight and safe display class", () => {
    const aiScore = 25;

    const highlight = classifyAiHighlight(aiScore);
    expect(highlight).toBeNull();

    const scoreClass = aiScoreClass(aiScore);
    expect(scoreClass).toBe("score-ai-low");

    const barColor = aiBarColor(aiScore);
    expect(barColor).toBe("#27ae60"); // green
  });

  test("medium AI score produces moderate highlight", () => {
    const aiScore = 55;

    const highlight = classifyAiHighlight(aiScore);
    expect(highlight).not.toBeNull();
    expect(highlight.label).toBe("possibly AI");

    const scoreClass = aiScoreClass(aiScore);
    expect(scoreClass).toBe("score-ai-medium");
  });

  test("credibility highlight inversely maps from AI score", () => {
    // High AI score → low credibility → highlight applied
    const highAi = classifyCredibilityHighlight(90);
    expect(highAi).not.toBeNull();
    expect(highAi.credibility).toBe(10);
    expect(highAi.level).toBe("very-low");

    // Low AI score → high credibility → no highlight
    const lowAi = classifyCredibilityHighlight(20);
    expect(lowAi).toBeNull(); // credibility 80 >= 70: no highlight
  });
});

// ============================================================================
// Integration: Cost estimation → confirmation pipeline
// ============================================================================

describe("Integration: Cost estimation end-to-end pipeline", () => {
  test("AI scan with Haiku model: estimation → formatting → confirmation check", () => {
    const textLength = 2000;
    const model = "claude-haiku-4-5-20251001";

    // Step 1: Estimate cost
    const estimate = estimateAiScanCost(textLength, model);
    expect(estimate.inputTokens).toBeGreaterThan(0);
    expect(estimate.outputTokens).toBeGreaterThan(0);
    expect(estimate.total).toBeGreaterThan(0);
    expect(estimate.label).toBe("Haiku 4.5");

    // Step 2: Format cost
    const formatted = formatCost(estimate.total);
    expect(formatted).toMatch(/^\$|^< \$/); // starts with $ or < $

    // Step 3: Verify the cost is very low for Haiku model
    expect(estimate.total).toBeLessThan(0.01); // Should be well under 1 cent
  });

  test("Opus model is significantly more expensive than Haiku", () => {
    const textLength = 4000;
    const haikuEst = estimateAiScanCost(textLength, "claude-haiku-4-5-20251001");
    const opusEst = estimateAiScanCost(textLength, "claude-opus-4-7");

    expect(opusEst.total).toBeGreaterThan(haikuEst.total);
    // Opus is 5x input price and 5x output price vs Haiku
    const ratio = opusEst.total / haikuEst.total;
    expect(ratio).toBeGreaterThan(3);
  });

  test("all-models estimation sums all three model costs", () => {
    const textLength = 3000;
    const allModelsEst = estimateAiScanCost(textLength, "all-models");

    const haikuCost = estimateAiScanCost(textLength, "claude-haiku-4-5-20251001").total;
    const sonnetCost = estimateAiScanCost(textLength, "claude-sonnet-4-6").total;
    const opusCost = estimateAiScanCost(textLength, "claude-opus-4-7").total;

    expect(allModelsEst.total).toBeCloseTo(haikuCost + sonnetCost + opusCost, 10);
    expect(allModelsEst.label).toContain("All Models");
  });

  test("cross-check cost includes topic extraction + credibility verification", () => {
    const textLength = 2500;
    const estimate = estimateCrossCheckCost(textLength, "claude-sonnet-4-6");

    expect(estimate.inputTokens).toBeGreaterThan(0);
    expect(estimate.outputTokens).toBeGreaterThan(0);
    expect(estimate.total).toBeGreaterThan(0);
    expect(estimate.desc).toContain("Cross-check");
  });

  test("highlight segments add to AI scan cost", () => {
    const textLength = 2000;
    const model = "claude-haiku-4-5-20251001";
    const baseEst = estimateAiScanCost(textLength, model);
    const withHighlight = estimateAiScanCost(textLength, model, {
      segmentCount: 20,
      totalSegmentChars: 5000,
    });

    expect(withHighlight.total).toBeGreaterThan(baseEst.total);
    expect(withHighlight.inputTokens).toBeGreaterThan(baseEst.inputTokens);
  });
});

// ============================================================================
// Integration: Security results → display pipeline
// ============================================================================

describe("Integration: Security check results → display pipeline", () => {
  test("security response with mixed severities produces correct score class", () => {
    // Simulate a security response with mixed results
    const results = [
      { severity: 0, title: "HTTPS" },         // Pass = 100
      { severity: 0, title: "SSL" },            // Pass = 100
      { severity: 1, title: "Headers" },        // Info = 80
      { severity: 2, title: "Phishing risk" },  // Warning = 0
    ];

    // Step 1: Calculate score (same formula as server)
    const score = results.reduce((sum, r) => {
      if (r.severity === 0) return sum + 100;
      if (r.severity === 1) return sum + 80;
      return sum;
    }, 0) / results.length;

    expect(score).toBe(70); // (100+100+80+0)/4

    // Step 2: Get score display class
    const displayClass = secScoreClass(score);
    expect(displayClass).toBe("score-yellow");

    // Step 3: Resolve each severity and get meta
    results.forEach((r) => {
      const sevName = resolveSeverity(r.severity);
      const meta = getSeverityMeta(sevName);
      expect(meta).toBeDefined();
      expect(meta.cls).toMatch(/severity-(pass|info|warning)/);
    });
  });

  test("all-pass security results produce green score", () => {
    const score = 100; // All checks passed
    const displayClass = secScoreClass(score);
    expect(displayClass).toBe("score-green");

    const miniClass = miniScoreClass(score, false);
    expect(miniClass).toBe("good");
  });

  test("all-warning security results produce red score", () => {
    const score = 0;
    const displayClass = secScoreClass(score);
    expect(displayClass).toBe("score-red");

    const miniClass = miniScoreClass(score, false);
    expect(miniClass).toBe("danger");
  });
});

// ============================================================================
// Integration: History management pipeline
// ============================================================================

describe("Integration: History entry lifecycle", () => {
  test("entries from different check types accumulate correctly", () => {
    let history = [];

    // Add security check result
    history = prependEntry(history, {
      type: "security",
      url: "https://example.com",
      score: 85,
      results: [{ title: "HTTPS", severity: "Pass" }],
    });

    // Add AI check result
    history = prependEntry(history, {
      type: "ai",
      url: "https://example.com",
      score: 45,
      results: [{ title: "Vocabulary Richness", aiScore: 45 }],
    });

    // Add cross-check result
    history = prependEntry(history, {
      type: "credibility",
      url: "https://example.com",
      score: 72,
      results: [{ title: "Cross-check", verdict: "Mostly Supported" }],
    });

    expect(history).toHaveLength(3);
    // Most recent first
    expect(history[0].type).toBe("credibility");
    expect(history[1].type).toBe("ai");
    expect(history[2].type).toBe("security");

    // All entries have timestamps
    history.forEach((entry) => {
      expect(entry.timestamp).toBeDefined();
      expect(typeof entry.timestamp).toBe("number");
    });
  });

  test("history trimming works with MAX_HISTORY entries", () => {
    let history = [];

    // Add MAX_HISTORY + 10 entries
    for (let i = 0; i < MAX_HISTORY + 10; i++) {
      history = prependEntry(history, {
        type: "security",
        url: `https://site${i}.com`,
        score: 80,
      });
    }

    // Should be capped at MAX_HISTORY
    expect(history.length).toBe(MAX_HISTORY);
    // Most recent entry should be the last one added
    expect(history[0].url).toBe(`https://site${MAX_HISTORY + 9}.com`);
  });

  test("score display classes apply correctly to history entries", () => {
    const entries = [
      { type: "security", score: 90 },
      { type: "security", score: 45 },
      { type: "ai", score: 80 },
      { type: "ai", score: 30 },
    ];

    const displayClasses = entries.map((e) => {
      if (e.type === "security") return secScoreClass(e.score);
      return aiScoreClass(e.score);
    });

    expect(displayClasses[0]).toBe("score-green");
    expect(displayClasses[1]).toBe("score-orange");
    expect(displayClasses[2]).toBe("score-ai-vhigh");
    expect(displayClasses[3]).toBe("score-ai-low");

    // Mini score classes for compact view
    const miniClasses = entries.map((e) =>
      miniScoreClass(e.score, e.type === "ai")
    );
    expect(miniClasses[0]).toBe("good");
    expect(miniClasses[1]).toBe("bad");
    expect(miniClasses[2]).toBe("danger"); // AI 80+ = danger (high AI probability)
    expect(miniClasses[3]).toBe("ok");     // AI 30 = ok (30 < 55 boundary)
  });
});

// ============================================================================
// Integration: Claim analysis → display pipeline
// ============================================================================

describe("Integration: Cross-check claims → classification pipeline", () => {
  test("mixed claims produce correct classification distribution", () => {
    const claims = [
      "Temperature rise: Supported - matches observed data",
      "Sea level claim: Contradicted - exaggerated by 3x",
      "Economic impact: Unverifiable - no sources available",
      "Timeline claim: Misleading - taken out of context",
      "Research funding: Supported - confirmed by multiple outlets",
    ];

    const classifications = claims.map(classifyClaim);

    expect(classifications.filter((c) => c === "claim-supported")).toHaveLength(2);
    expect(classifications.filter((c) => c === "claim-contradicted")).toHaveLength(2); // contradicted + misleading
    expect(classifications.filter((c) => c === "claim-unverifiable")).toHaveLength(1);
  });

  test("claim classification integrates with severity for overall assessment", () => {
    // Simulate: 5 reliable sources = Pass
    const reliableSourceCount = 5;
    const severity = categorizeSeverity(reliableSourceCount);
    expect(severity).toBe("Pass");

    const meta = getSeverityMeta(severity);
    expect(meta.cls).toBe("severity-pass");
    expect(meta.icon).toBe("✓");
  });
});

// ============================================================================
// Integration: AI + Security scores combined in mini score display
// ============================================================================

describe("Integration: Combined score dashboard display", () => {
  test("mini score classes correctly differentiate AI vs security interpretation", () => {
    // Same numeric score, different interpretation
    const score75 = 75;

    // Security 75 = ok (decent security, 60-79 range)
    expect(miniScoreClass(score75, false)).toBe("ok");
    // AI 75 = danger (>= 75 threshold)
    expect(miniScoreClass(score75, true)).toBe("danger");
  });

  test("full dashboard scenario with all three check types", () => {
    const securityScore = 88;
    const aiScore = 62;
    const credibilityScore = 71;

    // Security display
    expect(secScoreClass(securityScore)).toBe("score-green");
    expect(miniScoreClass(securityScore, false)).toBe("good");

    // AI display
    expect(aiScoreClass(aiScore)).toBe("score-ai-medium");
    expect(miniScoreClass(aiScore, true)).toBe("bad");
    expect(aiBarColor(aiScore)).toBe("#f1c40f"); // yellow - medium concern

    // Credibility display
    expect(infoBarColor(credibilityScore)).toBe("#2ecc71"); // green - good
  });
});
