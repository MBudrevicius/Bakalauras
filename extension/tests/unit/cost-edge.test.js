const { estimateAiScanCost, estimateCrossCheckCost, formatCost, MODEL_PRICING } = require("./cost-functions");

describe("estimateAiScanCost - edge cases", () => {
  test("zero text length returns zero-cost base (no detection)", () => {
    const est = estimateAiScanCost(0);
    expect(est.inputTokens).toBe(0);
    expect(est.outputTokens).toBe(0);
    expect(est.total).toBe(0);
  });

  test("very small text uses actual length (below cap)", () => {
    const est100 = estimateAiScanCost(100);
    const est200 = estimateAiScanCost(200);
    expect(est200.inputTokens).toBeGreaterThan(est100.inputTokens);
  });

  test("text length exactly at cap equals just over cap", () => {
    const atCap = estimateAiScanCost(4000);
    const overCap = estimateAiScanCost(4001);
    expect(atCap.inputTokens).toBe(overCap.inputTokens);
  });

  test("highlight with zero segments returns detection-only cost", () => {
    const withEmptyHL = estimateAiScanCost(1000, "claude-haiku-4-5-20251001", {
      segmentCount: 0,
      totalSegmentChars: 0,
    });
    const withoutHL = estimateAiScanCost(1000, "claude-haiku-4-5-20251001");
    expect(withEmptyHL.total).toBe(withoutHL.total);
  });

  test("highlight segment chars are capped at 12000", () => {
    const small = estimateAiScanCost(0, "claude-haiku-4-5-20251001", {
      segmentCount: 10,
      totalSegmentChars: 12000,
    });
    const huge = estimateAiScanCost(0, "claude-haiku-4-5-20251001", {
      segmentCount: 10,
      totalSegmentChars: 50000,
    });
    expect(small.inputTokens).toBe(huge.inputTokens);
  });

  test("highlight output tokens capped at 2048", () => {
    const est = estimateAiScanCost(0, "claude-haiku-4-5-20251001", {
      segmentCount: 500,
      totalSegmentChars: 5000,
    });
    expect(est.outputTokens).toBe(2048);
  });

  test("all-models with highlight", () => {
    const est = estimateAiScanCost(1000, "all-models", {
      segmentCount: 5,
      totalSegmentChars: 500,
    });
    expect(est.label).toContain("All Models");
    expect(est.total).toBeGreaterThan(0);
  });
});

describe("estimateCrossCheckCost - edge cases", () => {
  test("zero text length still has overhead from prompt constants", () => {
    const est = estimateCrossCheckCost(0);
    expect(est.inputTokens).toBeGreaterThan(0);
    expect(est.total).toBeGreaterThan(0);
  });

  test("all three models produce different costs", () => {
    const models = Object.keys(MODEL_PRICING);
    const costs = models.map((m) => estimateCrossCheckCost(1000, m).total);
    expect(new Set(costs).size).toBe(3);
  });

  test("larger text costs more up to cap", () => {
    const small = estimateCrossCheckCost(500);
    const large = estimateCrossCheckCost(3000);
    expect(large.total).toBeGreaterThan(small.total);
  });
});

describe("formatCost - edge cases", () => {
  test("exactly 0.001 formats normally", () => {
    expect(formatCost(0.001)).toBe("$0.0010");
  });

  test("large cost", () => {
    expect(formatCost(12.5)).toBe("$12.5000");
  });

  test("negative cost (edge case) returns < $0.001", () => {
    expect(formatCost(-0.5)).toBe("< $0.001");
  });
});
