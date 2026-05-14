const { getModelPricing, tokensFromChars, calcCost, estimateAiScanCost, estimateCrossCheckCost, formatCost, MODEL_PRICING } = require("./cost-functions");

describe("getModelPricing", () => {
  test("returns correct pricing for Haiku", () => {
    const p = getModelPricing("claude-haiku-4-5-20251001");
    expect(p.input).toBe(1.0);
    expect(p.output).toBe(5.0);
    expect(p.label).toBe("Haiku 4.5");
  });

  test("returns correct pricing for Sonnet", () => {
    const p = getModelPricing("claude-sonnet-4-6");
    expect(p.input).toBe(3.0);
    expect(p.output).toBe(15.0);
  });

  test("returns correct pricing for Opus", () => {
    const p = getModelPricing("claude-opus-4-7");
    expect(p.input).toBe(5.0);
    expect(p.output).toBe(25.0);
  });

  test("falls back to Haiku for unknown model", () => {
    const p = getModelPricing("unknown-model");
    expect(p.label).toBe("Haiku 4.5");
  });
});

describe("tokensFromChars", () => {
  test("calculates token count correctly", () => {
    expect(tokensFromChars(4)).toBe(1);
    expect(tokensFromChars(5)).toBe(2);
    expect(tokensFromChars(100)).toBe(25);
    expect(tokensFromChars(0)).toBe(0);
  });

  test("rounds up to nearest integer", () => {
    expect(tokensFromChars(1)).toBe(1);
    expect(tokensFromChars(3)).toBe(1);
    expect(tokensFromChars(7)).toBe(2);
  });
});

describe("calcCost", () => {
  test("calculates cost correctly for Haiku pricing", () => {
    const pricing = MODEL_PRICING["claude-haiku-4-5-20251001"];
    const cost = calcCost(1000, 500, pricing);
    expect(cost).toBeCloseTo(0.0035, 6);
  });

  test("returns 0 for zero tokens", () => {
    const pricing = MODEL_PRICING["claude-haiku-4-5-20251001"];
    expect(calcCost(0, 0, pricing)).toBe(0);
  });

  test("more expensive model costs more", () => {
    const haiku = MODEL_PRICING["claude-haiku-4-5-20251001"];
    const opus = MODEL_PRICING["claude-opus-4-7"];
    const costHaiku = calcCost(1000, 500, haiku);
    const costOpus = calcCost(1000, 500, opus);
    expect(costOpus).toBeGreaterThan(costHaiku);
  });
});

describe("estimateAiScanCost", () => {
  test("returns estimate with correct structure", () => {
    const est = estimateAiScanCost(1000, "claude-haiku-4-5-20251001");
    expect(est).toHaveProperty("inputTokens");
    expect(est).toHaveProperty("outputTokens");
    expect(est).toHaveProperty("total");
    expect(est).toHaveProperty("label");
    expect(est).toHaveProperty("desc");
  });

  test("returns positive cost for non-zero text", () => {
    const est = estimateAiScanCost(1000);
    expect(est.total).toBeGreaterThan(0);
    expect(est.inputTokens).toBeGreaterThan(0);
    expect(est.outputTokens).toBeGreaterThan(0);
  });

  test("caps text length at MAX_TEXT_LENGTH", () => {
    const small = estimateAiScanCost(1000);
    const huge = estimateAiScanCost(100000);
    const atMax = estimateAiScanCost(4000);
    expect(huge.inputTokens).toBe(atMax.inputTokens);
  });

  test("includes highlight cost when highlight param provided", () => {
    const withoutHL = estimateAiScanCost(1000);
    const withHL = estimateAiScanCost(1000, "claude-haiku-4-5-20251001", {
      segmentCount: 10,
      totalSegmentChars: 2000,
    });
    expect(withHL.total).toBeGreaterThan(withoutHL.total);
    expect(withHL.desc).toContain("highlight");
  });

  test("all-models returns combined cost for 3 models", () => {
    const single = estimateAiScanCost(1000, "claude-haiku-4-5-20251001");
    const all = estimateAiScanCost(1000, "all-models");
    expect(all.total).toBeGreaterThan(single.total);
    expect(all.label).toContain("All Models");
  });

  test("zero text length with highlight only counts highlight", () => {
    const est = estimateAiScanCost(0, "claude-haiku-4-5-20251001", {
      segmentCount: 5,
      totalSegmentChars: 500,
    });
    expect(est.desc).toContain("Highlight analysis");
    expect(est.total).toBeGreaterThan(0);
  });
});

describe("estimateCrossCheckCost", () => {
  test("returns positive cost", () => {
    const est = estimateCrossCheckCost(2000);
    expect(est.total).toBeGreaterThan(0);
  });

  test("includes both topic extraction and credibility tokens", () => {
    const est = estimateCrossCheckCost(2000);
    expect(est.outputTokens).toBe(550);
  });

  test("returns correct description", () => {
    const est = estimateCrossCheckCost(1000);
    expect(est.desc).toBe("Cross-check with credibility analysis");
  });

  test("uses correct model label", () => {
    const est = estimateCrossCheckCost(1000, "claude-opus-4-7");
    expect(est.label).toBe("Opus 4.7");
  });
});

describe("formatCost", () => {
  test("formats small costs as < $0.001", () => {
    expect(formatCost(0.0001)).toBe("< $0.001");
    expect(formatCost(0.0009)).toBe("< $0.001");
  });

  test("formats normal costs with 4 decimal places", () => {
    expect(formatCost(0.0035)).toBe("$0.0035");
    expect(formatCost(1.234)).toBe("$1.2340");
  });

  test("formats zero cost as < $0.001", () => {
    expect(formatCost(0)).toBe("< $0.001");
  });
});
