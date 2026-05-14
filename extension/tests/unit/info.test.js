import { computeCoverageScore, computeDiversityScore, computeOverallScore, infoBarColor, classifyClaim, categorizeSeverity } from "./info-functions";

describe("computeCoverageScore", () => {
  test("0 reliable sources → 0", () => expect(computeCoverageScore(0)).toBe(0));
  test("1 reliable source → 40", () => expect(computeCoverageScore(1)).toBe(40));
  test("2 reliable sources → 40", () => expect(computeCoverageScore(2)).toBe(40));
  test("3 reliable sources → 70", () => expect(computeCoverageScore(3)).toBe(70));
  test("4 reliable sources → 70", () => expect(computeCoverageScore(4)).toBe(70));
  test("5 reliable sources → 100", () => expect(computeCoverageScore(5)).toBe(100));
  test("10 reliable sources → 100", () => expect(computeCoverageScore(10)).toBe(100));
});

describe("computeDiversityScore", () => {
  test("0 domains → 0", () => expect(computeDiversityScore(0)).toBe(0));
  test("1 domain → 40", () => expect(computeDiversityScore(1)).toBe(40));
  test("3 domains → 70", () => expect(computeDiversityScore(3)).toBe(70));
  test("5 domains → 100", () => expect(computeDiversityScore(5)).toBe(100));
  test("8 domains → 100", () => expect(computeDiversityScore(8)).toBe(100));
});

describe("computeOverallScore", () => {
  test("with credScore: weighted 60/20/20", () => {
    expect(computeOverallScore(80, 100, 100)).toBe(88);
  });

  test("with credScore=0 still uses weighted formula", () => {
    expect(computeOverallScore(0, 100, 100)).toBe(40);
  });

  test("without credScore (null): 50/50 coverage+diversity", () => {
    expect(computeOverallScore(null, 70, 40)).toBe(55);
  });

  test("all zeros with cred", () => {
    expect(computeOverallScore(0, 0, 0)).toBe(0);
  });

  test("all zeros without cred", () => {
    expect(computeOverallScore(null, 0, 0)).toBe(0);
  });

  test("all 100 with cred", () => {
    expect(computeOverallScore(100, 100, 100)).toBe(100);
  });

  test("all 100 without cred", () => {
    expect(computeOverallScore(null, 100, 100)).toBe(100);
  });

  test("rounds to nearest integer", () => {
    expect(computeOverallScore(75, 40, 40)).toBe(61);
  });
});

describe("infoBarColor", () => {
  test("score >= 70 → green", () => expect(infoBarColor(70)).toBe("#2ecc71"));
  test("score 90 → green", () => expect(infoBarColor(90)).toBe("#2ecc71"));
  test("score 69 → yellow", () => expect(infoBarColor(69)).toBe("#f1c40f"));
  test("score 45 → yellow", () => expect(infoBarColor(45)).toBe("#f1c40f"));
  test("score 44 → orange", () => expect(infoBarColor(44)).toBe("#e67e22"));
  test("score 20 → orange", () => expect(infoBarColor(20)).toBe("#e67e22"));
  test("score 19 → red", () => expect(infoBarColor(19)).toBe("#e74c3c"));
  test("score 0 → red", () => expect(infoBarColor(0)).toBe("#e74c3c"));
});

describe("classifyClaim", () => {
  test("supported claim", () => expect(classifyClaim("This claim is Supported by sources")).toBe("claim-supported"));
  test("contradicted claim", () => expect(classifyClaim("Contradicted by evidence")).toBe("claim-contradicted"));
  test("misleading claim", () => expect(classifyClaim("This is Misleading")).toBe("claim-contradicted"));
  test("unverifiable claim", () => expect(classifyClaim("Cannot be verified")).toBe("claim-unverifiable"));
  test("empty string", () => expect(classifyClaim("")).toBe("claim-unverifiable"));
  test("case insensitive supported", () => expect(classifyClaim("SUPPORTED")).toBe("claim-supported"));
  test("case insensitive contradicted", () => expect(classifyClaim("CONTRADICTED")).toBe("claim-contradicted"));
});

describe("categorizeSeverity", () => {
  test("0 sources → Warning", () => expect(categorizeSeverity(0)).toBe("Warning"));
  test("1 source → Warning", () => expect(categorizeSeverity(1)).toBe("Warning"));
  test("2 sources → Warning", () => expect(categorizeSeverity(2)).toBe("Warning"));
  test("3 sources → Info", () => expect(categorizeSeverity(3)).toBe("Info"));
  test("4 sources → Info", () => expect(categorizeSeverity(4)).toBe("Info"));
  test("5 sources → Pass", () => expect(categorizeSeverity(5)).toBe("Pass"));
  test("10 sources → Pass", () => expect(categorizeSeverity(10)).toBe("Pass"));
});
