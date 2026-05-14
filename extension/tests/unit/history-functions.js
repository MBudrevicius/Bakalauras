
const MAX_HISTORY = 200;

function trimHistory(history) {
  if (history.length > MAX_HISTORY) history.length = MAX_HISTORY;
  return history;
}

function prependEntry(history, entry) {
  history.unshift({ ...entry, timestamp: Date.now() });
  return trimHistory(history);
}

module.exports = { MAX_HISTORY, trimHistory, prependEntry };
