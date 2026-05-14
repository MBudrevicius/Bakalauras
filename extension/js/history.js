const MAX_HISTORY = 200;

export async function saveToHistory(entry) {
  const { checkHistory = [] } = await chrome.storage.local.get("checkHistory");
  checkHistory.unshift({
    ...entry,
    timestamp: Date.now()
  });
  if (checkHistory.length > MAX_HISTORY) checkHistory.length = MAX_HISTORY;
  await chrome.storage.local.set({ checkHistory });
}

export async function getHistory() {
  const { checkHistory = [] } = await chrome.storage.local.get("checkHistory");
  return checkHistory;
}

export async function clearHistory() {
  await chrome.storage.local.remove("checkHistory");
}
