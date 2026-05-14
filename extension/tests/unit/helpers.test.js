import { secScoreClass, aiScoreClass, aiBarColor, miniScoreClass, severityMap } from "./helper-functions";

describe("secScoreClass", () => {
  test("returns score-green for scores >= 80", () => {
    expect(secScoreClass(80)).toBe("score-green");
    expect(secScoreClass(100)).toBe("score-green");
    expect(secScoreClass(95)).toBe("score-green");
  });

  test("returns score-yellow for scores 60-79", () => {
    expect(secScoreClass(60)).toBe("score-yellow");
    expect(secScoreClass(79)).toBe("score-yellow");
  });

  test("returns score-orange for scores 40-59", () => {
    expect(secScoreClass(40)).toBe("score-orange");
    expect(secScoreClass(59)).toBe("score-orange");
  });

  test("returns score-red for scores < 40", () => {
    expect(secScoreClass(0)).toBe("score-red");
    expect(secScoreClass(39)).toBe("score-red");
  });
});

describe("aiScoreClass", () => {
  test("returns score-ai-low for scores < 50", () => {
    expect(aiScoreClass(0)).toBe("score-ai-low");
    expect(aiScoreClass(49)).toBe("score-ai-low");
  });

  test("returns score-ai-medium for scores 50-64", () => {
    expect(aiScoreClass(50)).toBe("score-ai-medium");
    expect(aiScoreClass(64)).toBe("score-ai-medium");
  });

  test("returns score-ai-high for scores 65-79", () => {
    expect(aiScoreClass(65)).toBe("score-ai-high");
    expect(aiScoreClass(79)).toBe("score-ai-high");
  });

  test("returns score-ai-vhigh for scores >= 80", () => {
    expect(aiScoreClass(80)).toBe("score-ai-vhigh");
    expect(aiScoreClass(100)).toBe("score-ai-vhigh");
  });
});

describe("aiBarColor", () => {
  test("returns green for low AI scores", () => {
    expect(aiBarColor(0)).toBe("#27ae60");
    expect(aiBarColor(49)).toBe("#27ae60");
  });

  test("returns yellow for medium AI scores", () => {
    expect(aiBarColor(50)).toBe("#f1c40f");
    expect(aiBarColor(64)).toBe("#f1c40f");
  });

  test("returns orange for high AI scores", () => {
    expect(aiBarColor(65)).toBe("#e67e22");
    expect(aiBarColor(79)).toBe("#e67e22");
  });

  test("returns red for very high AI scores", () => {
    expect(aiBarColor(80)).toBe("#e74c3c");
    expect(aiBarColor(100)).toBe("#e74c3c");
  });
});

describe("miniScoreClass", () => {
  describe("AI mode (isAi = true, higher = worse)", () => {
    test("returns good for low AI probability", () => {
      expect(miniScoreClass(10, true)).toBe("good");
      expect(miniScoreClass(29, true)).toBe("good");
    });

    test("returns ok for moderate AI probability", () => {
      expect(miniScoreClass(30, true)).toBe("ok");
      expect(miniScoreClass(54, true)).toBe("ok");
    });

    test("returns bad for high AI probability", () => {
      expect(miniScoreClass(55, true)).toBe("bad");
      expect(miniScoreClass(74, true)).toBe("bad");
    });

    test("returns danger for very high AI probability", () => {
      expect(miniScoreClass(75, true)).toBe("danger");
      expect(miniScoreClass(100, true)).toBe("danger");
    });
  });

  describe("Security/credibility mode (isAi = false, higher = better)", () => {
    test("returns good for high security scores", () => {
      expect(miniScoreClass(80, false)).toBe("good");
      expect(miniScoreClass(100, false)).toBe("good");
    });

    test("returns ok for moderate scores", () => {
      expect(miniScoreClass(60, false)).toBe("ok");
      expect(miniScoreClass(79, false)).toBe("ok");
    });

    test("returns bad for low scores", () => {
      expect(miniScoreClass(40, false)).toBe("bad");
      expect(miniScoreClass(59, false)).toBe("bad");
    });

    test("returns danger for very low scores", () => {
      expect(miniScoreClass(0, false)).toBe("danger");
      expect(miniScoreClass(39, false)).toBe("danger");
    });
  });
});

describe("severityMap", () => {
  test("maps numeric values to severity strings", () => {
    expect(severityMap[0]).toBe("Pass");
    expect(severityMap[1]).toBe("Info");
    expect(severityMap[2]).toBe("Warning");
  });
});
