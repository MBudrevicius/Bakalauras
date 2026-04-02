# Web Checker - AI-Powered Security & Content Analysis

A full-stack web application that analyzes web pages for security threats and AI-generated content using local checks, Claude AI, and browser extension integration.

## 🎯 Features

- **Security Analysis**: HTTPS, SSL certificates, domain age, suspicious links, phishing detection
- **AI Content Detection**: Detects AI-generated text with multiple heuristics and optional Claude AI analysis
- **User Accounts**: Registration, login, credit-based payment system for premium features
- **Payment System**: Purchase credits for Claude AI checks (1 credit per check)
- **Structured Logging**: Full API request/response logging with Serilog + SEQ
- **Browser Extension**: Real-time security scores directly in your browser
- **Cross-Check**: Find related pages and compare stability across websites

## 🏗️ Architecture

```
Project/
├── extension/           # Browser extension (Chrome/Firefox)
│   ├── manifest.json
│   ├── popup.html/css/js
│   └── icons/
├── server/              # .NET 9 ASP.NET Core REST API
│   ├── Data/            # Database context & migrations
│   ├── Models/          # Domain models
│   ├── Services/        # Business logic
│   ├── Endpoints/       # API route handlers
│   ├── Middleware/      # Request logging
│   ├── AiChecks/        # AI detection algorithms
│   └── SecurityChecks/  # Security validators
├── LOGGING.md           # Logging configuration guide
└── README.md            # This file
```

## 🚀 Quick Start

### Prerequisites
- **.NET 9 SDK** or later
- **PostgreSQL** (NeonDB recommended for cloud, or local Postgres)
- **Standalone SEQ** (optional, for centralized logging at [datalust.io/seq](https://datalust.io/seq))

### 1. Configure Database

Update `server/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=your-host;Port=5432;Database=your_db;User Id=user;Password=pass;"
}
```

For **NeonDB Cloud**:
```
postgresql://user:password@host/database?sslmode=require
```

### 2. Configure JWT & API Keys

Update `server/appsettings.json` required fields:
```json
"Jwt": {
  "Secret": "your-secure-32-char-minimum-secret-key"
},
"Claude": {
  "ApiKey": "sk-ant-..."  // Get from Anthropic
}
```

### 3. Run Migrations & Start Server

```bash
cd server

# Restore packages
dotnet restore

# Apply database migrations
dotnet ef database update

# Start the server
dotnet run
```

Server runs on: `https://localhost:7XXX` (HTTPS) or `http://localhost:5XXX` (HTTP)

### 4. Install Browser Extension

1. Navigate to `extension/` folder in Chrome/Firefox
2. Load as unpacked extension
3. Configure API endpoint in popup settings

## 📚 API Endpoints

### Authentication
```
POST   /api/user/register              - Create account (free 5 credits)
POST   /api/user/login                 - Login & get JWT token
GET    /api/user/profile               - Get user profile (auth required)
GET    /api/user/credits               - Get credits balance (auth required)
```

### Security Checks
```
POST   /api/security-checks            - Analyze URL security (free)
GET    /api/security-checks/:url       - Get cached security score
```

### AI Content Checks
```
POST   /api/ai-checks                  - Run AI detection (auth required, 1 credit for Claude)
POST   /api/ai-checks/free             - Run free AI checks (no auth)
```

### Cross-Check
```
POST   /api/cross-check                - Find related pages & compare scores
```

### Payment
```
GET    /api/payment/credit-packages    - Available credit packages
POST   /api/payment/purchase           - Buy credits (auth required)
GET    /api/payment/transactions       - Payment history (auth required)
POST   /api/payment/admin/grant-credits - [DEV] Grant credits manually
```

### Info
```
GET    /api/info                       - API health & version info
```

## 💳 Credit System

| Package | Cost | Credits | Bonus |
|---------|------|---------|-------|
| Starter | $5 | 500 | - |
| Popular | $10 | 1,100 | +10% |
| Professional | $25 | 2,875 | +15% |
| Enterprise | $50 | 6,000 | +20% |

- **Free Checks**: Security analysis, basic AI patterns (0 credits)
- **Claude AI Check**: 1 credit per analysis
- **New Users**: 5 free credits on registration

## 📊 Logging

All API requests are logged with:
- Request method, path, query parameters
- Response status code
- Execution time (milliseconds)
- Client IP address
- User ID (if authenticated)

### Console Logging (Always On)
```
[2026-04-03 10:15:30.123 +00:00] [INF] API Request Started ...
[2026-04-03 10:15:30.234 +00:00] [INF] API Request Completed ...
```

### SEQ Dashboard (Optional)
1. Install standalone SEQ from [datalust.io/seq](https://datalust.io/seq)
2. Run SEQ locally (default: `http://localhost:5341`)
3. Access dashboard to search, filter, and analyze logs
4. Query examples: `Path = "/api/ai-checks"`, `StatusCode = 200`, `ElapsedMilliseconds > 500`

See [LOGGING.md](LOGGING.md) for detailed configuration.

## 🔐 Security

- **Passwords**: Hashed with BCrypt
- **API Auth**: JWT bearer tokens (configurable expiration)
- **HTTPS**: TLS 1.2+ in production
- **CORS**: Configured for browser extension origin
- **API Keys**: Store sensitive values in `appsettings.json` (git-ignored)

### Production Checklist
- [ ] Change JWT secret to strong random value (32+ chars)
- [ ] Configure HTTPS certificates
- [ ] Use environment variables for sensitive config
- [ ] Enable API rate limiting
- [ ] Set appropriate log levels (Warning+)
- [ ] Configure backup for Postgres database
- [ ] Review CORS policy
- [ ] Enable HSTS headers

## 🔗 External Integrations

| Service | Purpose | Config Key | Required |
|---------|---------|-----------|----------|
| **Anthropic Claude API** | AI-generated content detection | `Claude:ApiKey` | Optional (paid feature) |
| **Google Safe Browsing** | Phishing/malware detection | `GoogleSafeBrowsing:ApiKey` | Yes |
| **Google Custom Search** | Find related pages | `GoogleCustomSearch:ApiKey` + `Cx` | Optional |
| **PostgreSQL/NeonDB** | Data storage | `ConnectionStrings:DefaultConnection` | Yes |
| **SEQ** | Centralized logging | Serilog config | Optional |

## 📝 Database Schema

### Users Table
```sql
- Id (primary key)
- Email (unique)
- Username (unique)
- PasswordHash (BCrypt)
- Credits (decimal)
- IsActive (boolean)
- CreatedAt, LastLoginAt (timestamps)
```

### PageScores Table
```sql
- Id (primary key)
- UserId (foreign key, nullable)
- Url (string)
- Domain (string)
- SecurityScore (int 0-100)
- AiScore (int 0-100)
- LastChecked (timestamp)
- CheckCount (int)
```

### PaymentTransactions Table
```sql
- Id (primary key)
- UserId (foreign key)
- Amount (decimal USD)
- CreditsGranted (decimal)
- Status (pending/completed/failed)
- CreatedAt, CompletedAt (timestamps)
```

## 🧪 Development

### Run with Swagger UI
```bash
cd server
dotnet run
```
Open: `https://localhost:7XXX/swagger` (in development)

### Test Endpoints

```bash
# Register
curl -X POST http://localhost:5000/api/user/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","username":"testuser","password":"password123"}'

# Login
curl -X POST http://localhost:5000/api/user/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}'

# Check security (no auth)
curl -X POST http://localhost:5000/api/security-checks \
  -H "Content-Type: application/json" \
  -d '{"url":"https://example.com"}'

# Check AI (with token from login)
curl -X POST http://localhost:5000/api/ai-checks \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"text":"Some text to analyze","useClaudeAi":true}'
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add YourMigrationName

# Apply migrations
dotnet ef database update

# Revert last migration
dotnet ef migrations remove
```

## 🐛 Troubleshooting

### Database Connection Failed
- Verify PostgreSQL is running
- Check connection string in `appsettings.json`
- Run `dotnet ef database update` to create tables

### JWT Secret Not Configured
- Add `Jwt:Secret` key (32+ characters) to `appsettings.json`
- Restart the application

### SEQ Connection Errors (Non-Critical)
- SEQ is optional; logs still appear in console
- Install standalone SEQ or remove from `WriteTo` in `appsettings.json`
- See [LOGGING.md](LOGGING.md)

### Google API Key Issues
- Secure Browsing is required; AI checks are optional
- Get keys from [Google Cloud Console](https://console.cloud.google.com)
- Claude API key from [Anthropic Dashboard](https://console.anthropic.com)

## 📦 Dependencies

### Core
- `.NET 9 Framework`
- `ASP.NET Core Web API`
- `Entity Framework Core 9` (ORM)

### Database
- `Npgsql` (PostgreSQL adapter)

### Security
- `System.IdentityModel.Tokens.Jwt` (JWT)
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `BCrypt.Net-Next` (Password hashing)

### Logging
- `Serilog.AspNetCore`
- `Serilog.Sinks.Seq` (optional)
- `Serilog.Sinks.Console`

### Content Analysis
- `HtmlAgilityPack` (HTML parsing)
- `Whois` (Domain age checking)

## 📄 License

[Add your license here]

## 🤝 Contributing

[Add contribution guidelines]

## 📞 Support

For issues or questions:
- Check [LOGGING.md](LOGGING.md) for logging troubleshooting
- Review API endpoint documentation in Swagger UI
- Check server logs for detailed error messages

---

**Last Updated**: April 3, 2026  
**.NET Version**: 9.0  
**Database**: PostgreSQL 12+
