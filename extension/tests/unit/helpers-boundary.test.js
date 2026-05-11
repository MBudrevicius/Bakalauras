const {
  secScoreClass,
  aiScoreClass,
  aiBarColor,
  miniScoreClass,
} = require("./helper-functions");

// Boundary value testing for all classification functions

describe("secScoreClass - boundary values", () => {
  test.each([
    [100, "score-green"],
    [80, "score-green"],
    [79, "score-yellow"],
    [60, "score-yellow"],
    [59, "score-orange"],
    [40, "score-orange"],
    [39, "score-red"],
    [0, "score-red"],
    [-1, "score-red"],
  ])("secScoreClass(%i) = %s", (score, expected) => {
    expect(secScoreClass(score)).toBe(expected);
  });
});

describe("aiScoreClass - boundary values", () => {
  test.each([
    [0, "score-ai-low"],
    [49, "score-ai-low"],
    [50, "score-ai-medium"],
    [64, "score-ai-medium"],
    [65, "score-ai-high"],
    [79, "score-ai-high"],
    [80, "score-ai-vhigh"],
    [100, "score-ai-vhigh"],
  ])("aiScoreClass(%i) = %s", (score, expected) => {
    expect(aiScoreClass(score)).toBe(expected);
  });
});

describe("aiBarColor - boundary values", () => {
  test.each([
    [0, "#27ae60"],
    [49, "#27ae60"],
    [50, "#f1c40f"],
    [64, "#f1c40f"],
    [65, "#e67e22"],
    [79, "#e67e22"],
    [80, "#e74c3c"],
    [100, "#e74c3c"],
  ])("aiBarColor(%i) = %s", (score, expected) => {
    expect(aiBarColor(score)).toBe(expected);
  });
});

describe("miniScoreClass - all boundary values", () => {
  // AI mode boundaries
  test.each([
    [0, true, "good"],
    [29, true, "good"],
    [30, true, "ok"],
    [54, true, "ok"],
    [55, true, "bad"],
    [74, true, "bad"],
    [75, true, "danger"],
    [100, true, "danger"],
  ])("miniScoreClass(%i, isAi=%s) = %s", (score, isAi, expected) => {
    expect(miniScoreClass(score, isAi)).toBe(expected);
  });

  // Security mode boundaries
  test.each([
    [100, false, "good"],
    [80, false, "good"],
    [79, false, "ok"],
    [60, false, "ok"],
    [59, false, "bad"],
    [40, false, "bad"],
    [39, false, "danger"],
    [0, false, "danger"],
  ])("miniScoreClass(%i, isAi=%s) = %s", (score, isAi, expected) => {
    expect(miniScoreClass(score, isAi)).toBe(expected);
  });
});
