import { classifyAiHighlight, classifyCredibilityHighlight } from "./highlight-functions";

describe("classifyAiHighlight", () => {
  test("returns null for scores below 40", () => {
    expect(classifyAiHighlight(0)).toBeNull();
    expect(classifyAiHighlight(20)).toBeNull();
    expect(classifyAiHighlight(39)).toBeNull();
  });

  test("returns slight signal for scores 40-49", () => {
    const result = classifyAiHighlight(40);
    expect(result).not.toBeNull();
    expect(result.label).toBe("slight signal");
  });

  test("returns possibly AI for scores 50-64", () => {
    const result = classifyAiHighlight(50);
    expect(result.label).toBe("possibly AI");
    expect(classifyAiHighlight(64).label).toBe("possibly AI");
  });

  test("returns likely AI for scores 65-79", () => {
    const result = classifyAiHighlight(65);
    expect(result.label).toBe("likely AI");
    expect(classifyAiHighlight(79).label).toBe("likely AI");
  });

  test("returns very likely AI for scores >= 80", () => {
    const result = classifyAiHighlight(80);
    expect(result.label).toBe("very likely AI");
    expect(classifyAiHighlight(100).label).toBe("very likely AI");
  });
});

describe("classifyCredibilityHighlight", () => {
  test("returns null for high credibility (low AI score)", () => {
    expect(classifyCredibilityHighlight(20)).toBeNull();
    expect(classifyCredibilityHighlight(0)).toBeNull();
    expect(classifyCredibilityHighlight(30)).toBeNull();
  });

  test("returns medium-low for credibility 46-69", () => {
    const result = classifyCredibilityHighlight(40);
    expect(result).not.toBeNull();
    expect(result.level).toBe("medium-low");
    expect(result.credibility).toBe(60);
  });

  test("returns low for credibility 26-45", () => {
    const result = classifyCredibilityHighlight(60);
    expect(result.level).toBe("low");
    expect(result.credibility).toBe(40);
  });

  test("returns very-low for credibility <= 25", () => {
    const result = classifyCredibilityHighlight(80);
    expect(result.level).toBe("very-low");
    expect(result.credibility).toBe(20);
  });

  test("clamps credibility to 0-100 range", () => {
    const result = classifyCredibilityHighlight(110);
    expect(result.credibility).toBe(0);
    expect(result.level).toBe("very-low");
  });

  test("boundary: AI score 31 => credibility 69 => medium-low", () => {
    const result = classifyCredibilityHighlight(31);
    expect(result).not.toBeNull();
    expect(result.credibility).toBe(69);
    expect(result.level).toBe("medium-low");
  });

  test("boundary: AI score 30 => credibility 70 => null (no highlight)", () => {
    expect(classifyCredibilityHighlight(30)).toBeNull();
  });
});
