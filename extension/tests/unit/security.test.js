const { severityMeta, severityMap, resolveSeverity, getSeverityMeta } = require("./security-functions");

describe("severityMap", () => {
  test("0 → Pass", () => expect(severityMap[0]).toBe("Pass"));
  test("1 → Info", () => expect(severityMap[1]).toBe("Info"));
  test("2 → Warning", () => expect(severityMap[2]).toBe("Warning"));
  test("3 → undefined", () => expect(severityMap[3]).toBeUndefined());
});

describe("severityMeta", () => {
  test("Pass has correct class and icon", () => {
    expect(severityMeta.Pass.cls).toBe("severity-pass");
    expect(severityMeta.Pass.icon).toBe("✓");
  });
  test("Info has correct class and icon", () => {
    expect(severityMeta.Info.cls).toBe("severity-info");
    expect(severityMeta.Info.icon).toBe("i");
  });
  test("Warning has correct class and icon", () => {
    expect(severityMeta.Warning.cls).toBe("severity-warning");
    expect(severityMeta.Warning.icon).toBe("!");
  });
});

describe("resolveSeverity", () => {
  test("numeric 0 → Pass", () => expect(resolveSeverity(0)).toBe("Pass"));
  test("numeric 1 → Info", () => expect(resolveSeverity(1)).toBe("Info"));
  test("numeric 2 → Warning", () => expect(resolveSeverity(2)).toBe("Warning"));
  test("unknown numeric → Info", () => expect(resolveSeverity(99)).toBe("Info"));
  test("string Pass passes through", () => expect(resolveSeverity("Pass")).toBe("Pass"));
  test("string Warning passes through", () => expect(resolveSeverity("Warning")).toBe("Warning"));
  test("string Info passes through", () => expect(resolveSeverity("Info")).toBe("Info"));
});

describe("getSeverityMeta", () => {
  test("Pass → severity-pass meta", () => {
    expect(getSeverityMeta("Pass")).toEqual({ cls: "severity-pass", icon: "✓" });
  });
  test("Info → severity-info meta", () => {
    expect(getSeverityMeta("Info")).toEqual({ cls: "severity-info", icon: "i" });
  });
  test("Warning → severity-warning meta", () => {
    expect(getSeverityMeta("Warning")).toEqual({ cls: "severity-warning", icon: "!" });
  });
  test("unknown key falls back to Info", () => {
    expect(getSeverityMeta("Unknown")).toEqual({ cls: "severity-info", icon: "i" });
  });
});
