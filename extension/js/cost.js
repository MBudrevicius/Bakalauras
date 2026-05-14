const MODEL_PRICING = {
  "claude-haiku-4-5-20251001": { input: 1.00, output: 5.00, label: "Haiku 4.5" },
  "claude-sonnet-4-6":         { input: 3.00, output: 15.00, label: "Sonnet 4.6" },
  "claude-opus-4-7":           { input: 5.00, output: 25.00, label: "Opus 4.7" },
};
const DEFAULT_MODEL = "claude-haiku-4-5-20251001";
const CHARS_PER_TOKEN = 4;

const DETECT_AI_PROMPT_CHARS = 220;
const DETECT_AI_MAX_TOKENS = 60;
const HIGHLIGHT_PROMPT_CHARS = 240;
const EXTRACT_TOPIC_PROMPT_CHARS = 480;
const EXTRACT_TOPIC_MAX_TOKENS = 50;
const CREDIBILITY_PROMPT_CHARS = 600;
const CREDIBILITY_SOURCE_CHARS = 2400;
const CREDIBILITY_MAX_TOKENS = 500;
const MAX_TEXT_LENGTH = 4000;
const MAX_HIGHLIGHT_INPUT = 12000;

function getModelPricing(model) {
  return MODEL_PRICING[model] || MODEL_PRICING[DEFAULT_MODEL];
}

function tokensFromChars(chars) {
  return Math.ceil(chars / CHARS_PER_TOKEN);
}

function calcCost(inputTokens, outputTokens, pricing) {
  return (inputTokens / 1_000_000) * pricing.input + (outputTokens / 1_000_000) * pricing.output;
}
export function estimateAiScanCost(textLength, model = DEFAULT_MODEL, highlight = null) {
  if (model === "all-models") {
    return estimateAllModelsCost(textLength, highlight);
  }

  const pricing = getModelPricing(model);

  let totalInput = 0;
  let totalOutput = 0;
  let desc = "";

  if (textLength > 0) {
    const sampleLen = Math.min(textLength, MAX_TEXT_LENGTH);
    totalInput += tokensFromChars(DETECT_AI_PROMPT_CHARS + sampleLen);
    totalOutput += DETECT_AI_MAX_TOKENS;
    desc = "AI detection analysis";
  }

  if (highlight && highlight.segmentCount > 0) {
    const segChars = Math.min(highlight.totalSegmentChars, MAX_HIGHLIGHT_INPUT);
    totalInput += tokensFromChars(HIGHLIGHT_PROMPT_CHARS + segChars);
    totalOutput += Math.min(highlight.segmentCount * 8, 2048);
    desc = textLength > 0
      ? `AI detection + highlight (${highlight.segmentCount} paragraphs)`
      : `Highlight analysis (${highlight.segmentCount} paragraphs)`;
  }

  const total = calcCost(totalInput, totalOutput, pricing);
  return { inputTokens: totalInput, outputTokens: totalOutput, total, label: pricing.label, desc };
}

function estimateAllModelsCost(textLength, highlight) {
  let combinedTotal = 0;
  let combinedInput = 0;
  let combinedOutput = 0;

  for (const model of Object.keys(MODEL_PRICING)) {
    const est = estimateAiScanCost(textLength, model, highlight);
    combinedTotal += est.total;
    combinedInput += est.inputTokens;
    combinedOutput += est.outputTokens;
  }

  return {
    inputTokens: combinedInput,
    outputTokens: combinedOutput,
    total: combinedTotal,
    label: "All Models (Haiku + Sonnet + Opus)",
    desc: "AI detection with all 3 models"
  };
}
export function estimateCrossCheckCost(textLength, model = DEFAULT_MODEL) {
  const pricing = getModelPricing(model);
  const sampleLen = Math.min(textLength, MAX_TEXT_LENGTH);

  let inputTokens = tokensFromChars(EXTRACT_TOPIC_PROMPT_CHARS + sampleLen);
  let outputTokens = EXTRACT_TOPIC_MAX_TOKENS;

  inputTokens += tokensFromChars(CREDIBILITY_PROMPT_CHARS + sampleLen + CREDIBILITY_SOURCE_CHARS);
  outputTokens += CREDIBILITY_MAX_TOKENS;

  const total = calcCost(inputTokens, outputTokens, pricing);
  return { inputTokens, outputTokens, total, label: pricing.label, desc: "Cross-check with credibility analysis" };
}

export function formatCost(cost) {
  if (cost < 0.001) return "< $0.001";
  return "$" + cost.toFixed(4);
}

export async function confirmApiCost(estimate) {
  const modal = document.getElementById("cost-confirm-overlay");
  const descEl = document.getElementById("cost-desc");
  const detailEl = document.getElementById("cost-detail");

  descEl.textContent = estimate.desc;
  detailEl.innerHTML =
    `<span style="color:#d0d8f0;font-weight:600">${estimate.label}</span><br>` +
    `~${estimate.inputTokens.toLocaleString()} input · ~${estimate.outputTokens.toLocaleString()} output tokens<br>` +
    `Est. cost: <span style="color:#e0e4f0">${formatCost(estimate.total)}</span>`;

  modal.classList.remove("hidden");

  return new Promise(resolve => {
    const accept = document.getElementById("cost-accept-btn");
    const cancel = document.getElementById("cost-cancel-btn");
    const remember = document.getElementById("cost-skip-chk");

    function cleanup() {
      modal.classList.add("hidden");
      accept.removeEventListener("click", onAccept);
      cancel.removeEventListener("click", onCancel);
    }
    function onAccept() {
      if (remember.checked) {
        chrome.storage.local.set({ skipCostConfirm: true });
      }
      cleanup();
      resolve(true);
    }
    function onCancel() { cleanup(); resolve(false); }

    accept.addEventListener("click", onAccept);
    cancel.addEventListener("click", onCancel);
  });
}

export async function shouldConfirmCost() {
  const { skipCostConfirm } = await chrome.storage.local.get("skipCostConfirm");
  return !skipCostConfirm;
}
