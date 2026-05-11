# Secure Web

A Chrome browser extension that checks website security, detects AI-generated text, and cross-checks page information against other sources.

## Features

- **Security Checks** - analyzes the current page for HTTPS, SSL certificate validity, domain age, redirect chains, mixed content, security headers, phishing indicators, suspicious links, and Google Safe Browsing status.
- **AI Text Detection** - runs heuristic checks (vocabulary richness, sentence uniformity, repetitive phrasing, punctuation patterns, etc.) and optionally uses the Anthropic Claude API to estimate if page content or selected text was AI-generated. Can highlight individual paragraphs directly on the page.
- **Information Cross-Check** - extracts the page topic (via Claude or page title) and searches for related sources using the Brave Search API so users can verify information.
- **Check History** - stores past checks locally with filtering and search.
- **Domain Score Tracking** - maintains running average scores per domain on the server.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL](https://www.postgresql.org/) database (or a hosted service like Neon)
- [Google Chrome](https://www.google.com/chrome/) (or any Chromium-based browser)
- API keys (configured in `server/appsettings.json`):
  - [Google Safe Browsing API](https://developers.google.com/safe-browsing) key
  - [Brave Search API](https://brave.com/search/api/) key
- Optional: [Anthropic API](https://console.anthropic.com/) key (entered in the extension UI by the user)

## Setup

### 1. Clone the repository

```bash
git clone https://github.com/<your-username>/secure-web.git
cd secure-web
```

### 2. Configure the server

Edit `server/appsettings.json` and set your database connection string and API keys:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost; Database=secureweb; Username=postgres; Password=your_password;"
  },
  "GoogleSafeBrowsing": {
    "ApiKey": "YOUR_GOOGLE_SAFE_BROWSING_KEY"
  },
  "BraveSearch": {
    "ApiKey": "YOUR_BRAVE_SEARCH_KEY"
  }
}
```

### 3. Run the server

```bash
cd server
dotnet restore
dotnet ef database update
dotnet run
```

The server starts on `http://localhost:5101`.

### 4. Load the extension

1. Open Chrome and go to `chrome://extensions/`
2. Enable **Developer mode** (top-right toggle)
3. Click **Load unpacked**
4. Select the `extension/` folder from this repository

### 5. Use the extension

Click the Secure Web icon in the browser toolbar on any website. Use the tabs to run security checks, AI detection, or cross-check information.

To enable Claude AI analysis, enter your Anthropic API key in the extension settings and check "Use AI".

## Tech Stack

- **Backend:** C# / .NET 9, Entity Framework Core, PostgreSQL, Serilog
- **Frontend:** Chrome Extension (Manifest V3), vanilla JavaScript, CSS
- **APIs:** Anthropic Claude, Google Safe Browsing, Brave Search
