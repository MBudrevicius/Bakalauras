import { createServer } from "http";
import { readFile } from "fs";
import { resolve as _resolve, normalize, join, extname } from "path";

const EXTENSION_DIR = _resolve(__dirname, "../..");

const MIME_TYPES = {
  ".html": "text/html",
  ".js":   "application/javascript",
  ".css":  "text/css",
  ".json": "application/json",
  ".png":  "image/png",
  ".svg":  "image/svg+xml",
};
function startServer(port = 0) {
  return new Promise((resolve) => {
    const server = createServer((req, res) => {
      const safePath = normalize(req.url.split("?")[0]).replace(/^(\.\.[/\\])+/, "");
      const filePath = join(EXTENSION_DIR, safePath);

      if (!filePath.startsWith(EXTENSION_DIR)) {
        res.writeHead(403);
        res.end("Forbidden");
        return;
      }

      readFile(filePath, (err, data) => {
        if (err) {
          res.writeHead(404);
          res.end("Not found");
          return;
        }
        const ext = extname(filePath);
        res.writeHead(200, { "Content-Type": MIME_TYPES[ext] || "application/octet-stream" });
        res.end(data);
      });
    });
    server.listen(port, "127.0.0.1", () => {
      resolve(server);
    });
  });
}
function getChromeMockScript(serverOrigin) {
  return `
    (() => {
      const _store = {};

      window.chrome = {
        storage: {
          local: {
            get: async (keys) => {
              if (typeof keys === "string") {
                return { [keys]: _store[keys] };
              }
              const result = {};
              const keyArr = Array.isArray(keys) ? keys : Object.keys(keys || {});
              for (const k of keyArr) result[k] = _store[k];
              return result;
            },
            set: async (obj) => { Object.assign(_store, obj); },
            remove: async (key) => { delete _store[key]; },
          },
          session: {
            get: async (keys) => {
              if (typeof keys === "string") return { [keys]: _store["__session_" + keys] };
              const result = {};
              const keyArr = Array.isArray(keys) ? keys : Object.keys(keys || {});
              for (const k of keyArr) result[k] = _store["__session_" + k];
              return result;
            },
            set: async (obj) => {
              for (const [k, v] of Object.entries(obj)) _store["__session_" + k] = v;
            },
          },
          onChanged: { addListener: () => {} },
        },
        tabs: {
          query: async () => [{ id: 1, url: "https://example.com/test-page" }],
          get:   async () => ({ id: 1, url: "https://example.com/test-page" }),
        },
        runtime: {
          getURL: (p) => "${serverOrigin}/" + p,
        },
        windows: {
          create: async () => {},
        },
        scripting: {
          executeScript: async () => [{ result: "Sample page text for testing." }],
        },
      };
    })();
  `;
}
const API_RESPONSES = {
  "POST /api/security-checks": {
    overallScore: 82,
    results: [
      { title: "HTTPS", description: "Site uses HTTPS.", severity: 0 },
      { title: "SSL Certificate", description: "Valid certificate.", severity: 0 },
      { title: "Security Headers", description: "Missing CSP header.", severity: 2 },
    ],
  },
  "POST /api/ai-checks": {
    overallAiScore: 67,
    results: [
      { title: "Hedging Language", description: "Moderate hedging detected.", aiScore: 55 },
      { title: "Repetitive Phrasing", description: "Some repetition.", aiScore: 72 },
    ],
  },
  "POST /api/ai-checks/all-models": {
    modelResults: [
      { modelLabel: "Haiku 4.5", overallAiScore: 60, results: [] },
      { modelLabel: "Sonnet 4.6", overallAiScore: 70, results: [] },
    ],
    blendedAiScore: 65,
    results: [],
  },
  "POST /api/cross-check": {
    credibility: { score: 75, verdict: "Mostly Supported", claims: ["Claim supported by sources"] },
    relatedPages: [
      { title: "Reuters article", url: "https://reuters.com/a", snippet: "Related article text" },
    ],
    sourceReliability: [
      { domain: "reuters.com", score: 80, evidence: "Major news agency" },
    ],
    pageLinkDomains: 3,
  },
  "GET /api/page-score": {
    securityScore: 80,
    credibilityScore: 70,
    aiScore: 45,
    securityCheckCount: 1,
    credibilityCheckCount: 1,
    aiCheckCount: 1,
  },
};
function getFetchMockScript() {
  return `
    (() => {
      const _apiResponses = ${JSON.stringify(API_RESPONSES)};
      const _origFetch = window.fetch;

      window.fetch = async function(url, opts = {}) {
        const urlStr = typeof url === "string" ? url : url.toString();
        if (urlStr.includes("/api/")) {
          const method = (opts.method || "GET").toUpperCase();
          const pathname = new URL(urlStr).pathname;
          const key = method + " " + pathname;
          const body = _apiResponses[key];
          if (body) {
            return new Response(JSON.stringify(body), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            });
          }
          return new Response(JSON.stringify({ error: "Not found" }), { status: 404 });
        }
        return _origFetch.call(this, url, opts);
      };
    })();
  `;
}

export default { startServer, getChromeMockScript, getFetchMockScript, API_RESPONSES };
