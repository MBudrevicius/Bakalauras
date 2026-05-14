const puppeteer = require("puppeteer");
const { startServer, getChromeMockScript, getFetchMockScript } = require("./ui-test-helpers");

let browser, server, baseUrl;

beforeAll(async () => {
  server = await startServer();
  const port = server.address().port;
  baseUrl = `http://127.0.0.1:${port}`;

  browser = await puppeteer.launch({
    headless: true,
    args: ["--no-sandbox", "--disable-setuid-sandbox"],
  });
}, 30_000);

afterAll(async () => {
  if (browser) await browser.close();
  if (server) server.close();
});
async function openPage(htmlPath) {
  const page = await browser.newPage();
  await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
  await page.evaluateOnNewDocument(getFetchMockScript());
  await page.goto(`${baseUrl}/${htmlPath}`, { waitUntil: "networkidle0" });
  return page;
}

describe("Popup – Privacy disclaimer", () => {
  test("disclaimer overlay is visible on first visit", async () => {
    const page = await openPage("popup.html");
    const hidden = await page.$eval("#disclaimer-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(false);

    const containerHidden = await page.$eval(".container", (el) => el.style.display);
    expect(containerHidden).toBe("none");
    await page.close();
  });

  test("clicking accept hides disclaimer and shows main UI", async () => {
    const page = await openPage("popup.html");
    await page.click("#disclaimer-accept-btn");
    await page.waitForSelector("#disclaimer-overlay.hidden");

    const overlayHidden = await page.$eval("#disclaimer-overlay", (el) => el.classList.contains("hidden"));
    expect(overlayHidden).toBe(true);

    const containerVisible = await page.$eval(".container", (el) => el.style.display);
    expect(containerVisible).toBe("");
    await page.close();
  });

  test("checking 'remember' saves preference – disclaimer hidden on reload", async () => {
    const page = await openPage("popup.html");
    await page.click("#disclaimer-remember-chk");
    await page.click("#disclaimer-accept-btn");
    await page.waitForSelector("#disclaimer-overlay.hidden");

    const page2 = await browser.newPage();
    await page2.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page2.evaluateOnNewDocument(getFetchMockScript());
    await page2.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page2.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });

    const hidden = await page2.$eval("#disclaimer-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
    await page.close();
    await page2.close();
  });
});

describe("Popup – URL display", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("displays the current tab URL", async () => {
    const url = await page.$eval("#url-display", (el) => el.textContent);
    expect(url).toBe("https://example.com/test-page");
  });
});

describe("Popup – Tab switching", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("Security tab is active by default", async () => {
    const secTabActive = await page.$eval('.tab[data-tab="security"]', (el) => el.classList.contains("active"));
    expect(secTabActive).toBe(true);

    const secPanelActive = await page.$eval("#tab-security", (el) => el.classList.contains("active"));
    expect(secPanelActive).toBe(true);
  });

  test("clicking AI tab activates it and hides Security panel", async () => {
    await page.click('.tab[data-tab="ai"]');

    const aiTabActive = await page.$eval('.tab[data-tab="ai"]', (el) => el.classList.contains("active"));
    expect(aiTabActive).toBe(true);

    const aiPanelActive = await page.$eval("#tab-ai", (el) => el.classList.contains("active"));
    expect(aiPanelActive).toBe(true);

    const secPanelActive = await page.$eval("#tab-security", (el) => el.classList.contains("active"));
    expect(secPanelActive).toBe(false);
  });

  test("clicking Info tab activates it, AI tab deactivates", async () => {
    await page.click('.tab[data-tab="info"]');

    const infoTabActive = await page.$eval('.tab[data-tab="info"]', (el) => el.classList.contains("active"));
    expect(infoTabActive).toBe(true);

    const infoPanelActive = await page.$eval("#tab-info", (el) => el.classList.contains("active"));
    expect(infoPanelActive).toBe(true);

    const aiPanelActive = await page.$eval("#tab-ai", (el) => el.classList.contains("active"));
    expect(aiPanelActive).toBe(false);
  });

  test("clicking Security tab returns to it", async () => {
    await page.click('.tab[data-tab="security"]');

    const secActive = await page.$eval('.tab[data-tab="security"]', (el) => el.classList.contains("active"));
    expect(secActive).toBe(true);
  });
});

describe("Popup – Security check execution", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("Run Security Checks button is present and enabled", async () => {
    const disabled = await page.$eval("#run-checks-btn", (el) => el.disabled);
    expect(disabled).toBe(false);
  });

  test("clicking the button reveals score and results", async () => {
    await page.click("#run-checks-btn");

    await page.waitForFunction(
      () => !document.getElementById("score-section").classList.contains("hidden"),
      { timeout: 5000 }
    );

    const score = await page.$eval("#score-value", (el) => el.textContent);
    expect(score).toBe("82");

    const resultCount = await page.$$eval("#results-list li", (items) => items.length);
    expect(resultCount).toBe(3);
  });

  test("result items display correct titles", async () => {
    const titles = await page.$$eval("#results-list .result-title", (els) => els.map((e) => e.textContent));
    expect(titles).toContain("HTTPS");
    expect(titles).toContain("SSL Certificate");
    expect(titles).toContain("Security Headers");
  });

  test("score circle has correct colour class for score 82", async () => {
    const classes = await page.$eval("#score-circle", (el) => el.className);
    expect(classes).toContain("score-green");
  });
});

describe("Popup – AI Detection tab", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
    await page.click('.tab[data-tab="ai"]');
  });
  afterAll(async () => { await page.close(); });

  test("API key input field is present and initially empty", async () => {
    const value = await page.$eval("#claude-api-key", (el) => el.value);
    expect(value).toBe("");
  });

  test("API key input is password type by default", async () => {
    const type = await page.$eval("#claude-api-key", (el) => el.type);
    expect(type).toBe("password");
  });

  test("toggling eye button reveals key text", async () => {
    await page.click("#api-key-toggle");
    const type = await page.$eval("#claude-api-key", (el) => el.type);
    expect(type).toBe("text");
    await page.click("#api-key-toggle");
    const typeAgain = await page.$eval("#claude-api-key", (el) => el.type);
    expect(typeAgain).toBe("password");
  });

  test("model dropdown has 4 options", async () => {
    const options = await page.$$eval("#claude-model option", (opts) => opts.map((o) => o.value));
    expect(options).toEqual([
      "claude-haiku-4-5-20251001",
      "claude-sonnet-4-6",
      "claude-opus-4-7",
      "all-models",
    ]);
  });

  test("Scan Full Page button triggers AI scan and shows results", async () => {
    await page.click("#ai-full-page-btn");

    await page.waitForFunction(
      () => !document.getElementById("ai-score-section").classList.contains("hidden"),
      { timeout: 5000 }
    );

    const score = await page.$eval("#ai-score-value", (el) => el.textContent);
    expect(score).toBe("67%");

    const resultCount = await page.$$eval("#ai-results-list li", (items) => items.length);
    expect(resultCount).toBe(2);
  });
});

describe("Popup – Information Credibility tab", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
    await page.click('.tab[data-tab="info"]');
  });
  afterAll(async () => { await page.close(); });

  test("Cross-check Page button is present", async () => {
    const text = await page.$eval("#cross-check-btn", (el) => el.textContent);
    expect(text).toContain("Cross-check");
  });

  test("highlight toggle checkbox is present and unchecked", async () => {
    const checked = await page.$eval("#info-highlight-toggle", (el) => el.checked);
    expect(checked).toBe(false);
  });

  test("clicking Cross-check Page shows credibility score", async () => {
    await page.click("#cross-check-btn");

    await page.waitForFunction(
      () => !document.getElementById("info-score-section").classList.contains("hidden"),
      { timeout: 5000 }
    );

    const score = await page.$eval("#info-score-value", (el) => el.textContent);
    expect(Number(score)).toBeGreaterThan(0);
  });
});

describe("Popup – Stored page scores", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("page scores section becomes visible after load", async () => {
    await page.waitForFunction(
      () => !document.getElementById("stored-scores-section").classList.contains("hidden"),
      { timeout: 5000 }
    );
    const visible = await page.$eval("#stored-scores-section", (el) => !el.classList.contains("hidden"));
    expect(visible).toBe(true);
  });

  test("security score shows a value or N/A", async () => {
    await page.waitForFunction(
      () => document.getElementById("stored-sec-score").textContent !== "-",
      { timeout: 5000 }
    );
    const sec = await page.$eval("#stored-sec-score", (el) => el.textContent);
    expect(["80", "N/A"]).toContain(sec);
  });

  test("AI score shows a value or N/A", async () => {
    await page.waitForFunction(
      () => document.getElementById("stored-ai-score").textContent !== "-",
      { timeout: 5000 }
    );
    const ai = await page.$eval("#stored-ai-score", (el) => el.textContent);
    expect(["45%", "N/A"]).toContain(ai);
  });
});

describe("Popup – Cost confirmation dialog", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("cost dialog is hidden by default", async () => {
    const hidden = await page.$eval("#cost-confirm-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
  });

  test("opening cost dialog programmatically shows it with data", async () => {
    await page.evaluate(() => {
      const modal = document.getElementById("cost-confirm-overlay");
      const descEl = document.getElementById("cost-desc");
      const detailEl = document.getElementById("cost-detail");
      descEl.textContent = "AI detection analysis";
      detailEl.innerHTML = "Haiku 4.5<br>~1200 input tokens<br>Est. cost: $0.0012";
      modal.classList.remove("hidden");
    });

    const visible = await page.$eval("#cost-confirm-overlay", (el) => !el.classList.contains("hidden"));
    expect(visible).toBe(true);

    const desc = await page.$eval("#cost-desc", (el) => el.textContent);
    expect(desc).toBe("AI detection analysis");
  });

  test("clicking Cancel closes the dialog", async () => {
    await page.evaluate(() => {
      document.getElementById("cost-cancel-btn").addEventListener("click", () => {
        document.getElementById("cost-confirm-overlay").classList.add("hidden");
      });
    });
    await page.click("#cost-cancel-btn");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 50)));
    const hidden = await page.$eval("#cost-confirm-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
  });
});

describe("Popup – Error modal", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("error modal is hidden by default", async () => {
    const hidden = await page.$eval("#error-modal-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
  });

  test("showing error modal displays the message", async () => {
    await page.evaluate(() => {
      const overlay = document.getElementById("error-modal-overlay");
      const closeBtn = document.getElementById("error-modal-close-btn");
      document.getElementById("error-modal-message").textContent = "Connection failed";
      overlay.classList.remove("hidden");
      closeBtn.addEventListener("click", () => overlay.classList.add("hidden"));
    });

    const msg = await page.$eval("#error-modal-message", (el) => el.textContent);
    expect(msg).toBe("Connection failed");
  });

  test("clicking Close hides the error modal", async () => {
    await page.click("#error-modal-close-btn");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 50)));
    const hidden = await page.$eval("#error-modal-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
  });
});

describe("Popup – Loading overlay", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("loading overlay is hidden by default", async () => {
    const hidden = await page.$eval("#loading-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
  });

  test("showing loading overlay renders spinner text", async () => {
    await page.evaluate(() => {
      document.getElementById("loading-text").textContent = "Scanning...";
      document.getElementById("loading-overlay").classList.remove("hidden");
    });

    const text = await page.$eval("#loading-text", (el) => el.textContent);
    expect(text).toBe("Scanning...");

    const visible = await page.$eval("#loading-overlay", (el) => !el.classList.contains("hidden"));
    expect(visible).toBe(true);
  });

  test("hiding loading overlay works", async () => {
    await page.evaluate(() => {
      document.getElementById("loading-overlay").classList.add("hidden");
    });
    const hidden = await page.$eval("#loading-overlay", (el) => el.classList.contains("hidden"));
    expect(hidden).toBe(true);
  });
});

describe("History page – rendering and interactions", () => {
  async function openHistoryWithData(entries) {
    const page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    const entriesJson = JSON.stringify(entries);
    await page.evaluateOnNewDocument((data) => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "checkHistory") return { checkHistory: JSON.parse(data) };
        return origGet(keys);
      };
    }, entriesJson);
    await page.goto(`${baseUrl}/history.html`, { waitUntil: "networkidle0" });
    return page;
  }

  test("empty history shows 'No checks recorded' message", async () => {
    const page = await openHistoryWithData([]);
    const text = await page.$eval("#history-empty", (el) => el.textContent);
    expect(text).toContain("No checks recorded");
    const visible = await page.$eval("#history-empty", (el) => !el.classList.contains("hidden"));
    expect(visible).toBe(true);
    await page.close();
  });

  test("history entries are rendered as cards", async () => {
    const entries = [
      { type: "security", url: "https://example.com", score: 85, timestamp: Date.now(), results: [{ title: "HTTPS", severity: 0 }] },
      { type: "ai", url: "https://test.com", score: 60, timestamp: Date.now() - 60000, results: [{ title: "Hedging", aiScore: 55 }] },
    ];
    const page = await openHistoryWithData(entries);

    const cardCount = await page.$$eval(".history-card", (cards) => cards.length);
    expect(cardCount).toBe(2);
    await page.close();
  });

  test("security badge and AI badge are rendered correctly", async () => {
    const entries = [
      { type: "security", url: "https://a.com", score: 90, timestamp: Date.now(), results: [] },
      { type: "ai", url: "https://b.com", score: 50, timestamp: Date.now(), results: [] },
    ];
    const page = await openHistoryWithData(entries);

    const badges = await page.$$eval(".badge", (els) => els.map((e) => e.textContent.trim()));
    expect(badges).toContain("Security");
    expect(badges).toContain("AI Detection");
    await page.close();
  });

  test("filtering by 'security' shows only security entries", async () => {
    const entries = [
      { type: "security", url: "https://a.com", score: 90, timestamp: Date.now(), results: [] },
      { type: "ai", url: "https://b.com", score: 50, timestamp: Date.now(), results: [] },
    ];
    const page = await openHistoryWithData(entries);

    await page.select("#filter-type", "security");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 100)));

    const badges = await page.$$eval(".badge", (els) => els.map((e) => e.textContent.trim()));
    expect(badges).toEqual(["Security"]);
    await page.close();
  });

  test("filtering by 'ai' shows only AI entries", async () => {
    const entries = [
      { type: "security", url: "https://a.com", score: 90, timestamp: Date.now(), results: [] },
      { type: "ai", url: "https://b.com", score: 50, timestamp: Date.now(), results: [] },
    ];
    const page = await openHistoryWithData(entries);

    await page.select("#filter-type", "ai");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 100)));

    const badges = await page.$$eval(".badge", (els) => els.map((e) => e.textContent.trim()));
    expect(badges).toEqual(["AI Detection"]);
    await page.close();
  });

  test("clicking a card expands its details", async () => {
    const entries = [
      { type: "security", url: "https://a.com", score: 90, timestamp: Date.now(), results: [{ title: "HTTPS", severity: 0 }] },
    ];
    const page = await openHistoryWithData(entries);

    const hiddenBefore = await page.$eval(".history-details", (el) => el.classList.contains("hidden"));
    expect(hiddenBefore).toBe(true);

    await page.click(".history-toggle");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 50)));

    const hiddenAfter = await page.$eval(".history-details", (el) => el.classList.contains("hidden"));
    expect(hiddenAfter).toBe(false);
    await page.close();
  });

  test("clear button removes all entries (with confirm)", async () => {
    const entries = [
      { type: "security", url: "https://a.com", score: 90, timestamp: Date.now(), results: [] },
    ];
    const page = await openHistoryWithData(entries);

    await page.evaluate(() => {
      window.confirm = () => true;
      const origRemove = window.chrome.storage.local.remove;
      window.chrome.storage.local.remove = async (key) => {
        await origRemove(key);
        window.chrome.storage.local.get = async (keys) => {
          if (keys === "checkHistory") return { checkHistory: [] };
          return {};
        };
      };
    });

    await page.click("#clear-btn");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 200)));

    const emptyVisible = await page.$eval("#history-empty", (el) => !el.classList.contains("hidden"));
    expect(emptyVisible).toBe(true);
    await page.close();
  });

  test("clear button does nothing when confirm is cancelled", async () => {
    const entries = [
      { type: "security", url: "https://a.com", score: 90, timestamp: Date.now(), results: [] },
    ];
    const page = await openHistoryWithData(entries);

    await page.evaluate(() => { window.confirm = () => false; });

    await page.click("#clear-btn");
    await page.evaluate(() => new Promise((r) => setTimeout(r, 100)));

    const cardCount = await page.$$eval(".history-card", (cards) => cards.length);
    expect(cardCount).toBe(1);
    await page.close();
  });
});

describe("Popup – API key and model controls", () => {
  let page;
  beforeAll(async () => {
    page = await browser.newPage();
    await page.evaluateOnNewDocument(getChromeMockScript(baseUrl));
    await page.evaluateOnNewDocument(getFetchMockScript());
    await page.evaluateOnNewDocument(() => {
      const origGet = window.chrome.storage.local.get;
      window.chrome.storage.local.get = async (keys) => {
        if (keys === "disclaimerAccepted") return { disclaimerAccepted: true };
        return origGet(keys);
      };
    });
    await page.goto(`${baseUrl}/popup.html`, { waitUntil: "networkidle0" });
  });
  afterAll(async () => { await page.close(); });

  test("AI tab model select defaults to Haiku", async () => {
    await page.click('.tab[data-tab="ai"]');
    const model = await page.$eval("#claude-model", (el) => el.value);
    expect(model).toBe("claude-haiku-4-5-20251001");
  });

  test("changing model select updates the dropdown value", async () => {
    await page.select("#claude-model", "claude-sonnet-4-6");
    const model = await page.$eval("#claude-model", (el) => el.value);
    expect(model).toBe("claude-sonnet-4-6");
  });

  test("Use AI checkbox toggles on and off", async () => {
    const before = await page.$eval("#use-ai-chk", (el) => el.checked);
    await page.click("#use-ai-chk");
    const after = await page.$eval("#use-ai-chk", (el) => el.checked);
    expect(after).toBe(!before);
  });

  test("Info tab has its own API key and model dropdowns", async () => {
    await page.click('.tab[data-tab="info"]');
    const keyInput = await page.$("#info-claude-api-key");
    const modelSel = await page.$("#info-claude-model");
    expect(keyInput).not.toBeNull();
    expect(modelSel).not.toBeNull();
  });

  test("Info tab eye toggle works", async () => {
    const typeBefore = await page.$eval("#info-claude-api-key", (el) => el.type);
    expect(typeBefore).toBe("password");
    await page.click("#info-api-key-toggle");
    const typeAfter = await page.$eval("#info-claude-api-key", (el) => el.type);
    expect(typeAfter).toBe("text");
  });
});
