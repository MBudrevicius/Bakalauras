const { MAX_HISTORY, trimHistory, prependEntry } = require("./history-functions");

describe("MAX_HISTORY", () => {
  test("is 200", () => expect(MAX_HISTORY).toBe(200));
});

describe("trimHistory", () => {
  test("does not trim when under limit", () => {
    const history = [1, 2, 3];
    expect(trimHistory(history)).toHaveLength(3);
  });

  test("does not trim at exactly MAX_HISTORY", () => {
    const history = Array.from({ length: 200 }, (_, i) => i);
    expect(trimHistory(history)).toHaveLength(200);
  });

  test("trims to MAX_HISTORY when over", () => {
    const history = Array.from({ length: 205 }, (_, i) => i);
    expect(trimHistory(history)).toHaveLength(200);
  });

  test("keeps first items after trimming", () => {
    const history = Array.from({ length: 202 }, (_, i) => i);
    trimHistory(history);
    expect(history[0]).toBe(0);
    expect(history[199]).toBe(199);
  });

  test("empty array stays empty", () => {
    expect(trimHistory([])).toHaveLength(0);
  });
});

describe("prependEntry", () => {
  test("adds entry at beginning", () => {
    const history = [{ url: "old" }];
    const result = prependEntry(history, { url: "new", type: "security" });
    expect(result[0].url).toBe("new");
    expect(result[0].type).toBe("security");
    expect(result[1].url).toBe("old");
  });

  test("adds timestamp", () => {
    const before = Date.now();
    const result = prependEntry([], { url: "test" });
    const after = Date.now();
    expect(result[0].timestamp).toBeGreaterThanOrEqual(before);
    expect(result[0].timestamp).toBeLessThanOrEqual(after);
  });

  test("does not mutate original entry", () => {
    const entry = { url: "test" };
    prependEntry([], entry);
    expect(entry.timestamp).toBeUndefined();
  });

  test("trims when exceeding MAX_HISTORY", () => {
    const history = Array.from({ length: 200 }, (_, i) => ({ id: i }));
    const result = prependEntry(history, { url: "newest" });
    expect(result).toHaveLength(200);
    expect(result[0].url).toBe("newest");
  });

  test("preserves all entry properties", () => {
    const entry = { type: "ai", url: "https://example.com", score: 75 };
    const result = prependEntry([], entry);
    expect(result[0].type).toBe("ai");
    expect(result[0].url).toBe("https://example.com");
    expect(result[0].score).toBe(75);
  });
});
