# SEQ Logging Setup

This project uses **Serilog** with **SEQ** for structured logging. Both console logging and SEQ sink are configured.

## Features

- **Structured Logging**: All logs are JSON-serialized with rich context
- **Request Logging Middleware**: Automatically logs all incoming API requests with:
  - Request method, path, and query string
  - Response status code
  - Execution time (milliseconds)
  - Request ID (trace identifier)
  - Client IP address
- **SEQ Integration**: Optional centralized log aggregation dashboard
- **Console Output**: Real-time colored logs in console for development

## Quick Start (Local SEQ)

### Option 1: Docker (Recommended)

```bash
# Start SEQ locally with Docker Compose
docker-compose up -d

# SEQ dashboard will be available at: http://localhost:5341
```

### Option 2: Manual Installation

Download and install SEQ from [https://datalust.io/seq](https://datalust.io/seq) and run locally.

## Configuration

SEQ configuration is in `appsettings.json`:

```json
"Serilog": {
  "WriteTo": [
    {
      "Name": "Seq",
      "Args": {
        "serverUrl": "http://localhost:5341",  // Change if SEQ runs elsewhere
        "apiKey": null                            // Add API key if SEQ requires authentication
      }
    }
  ]
}
```

## How It Works

### Application Startup
- All startup events are logged (migrations, configurations, etc.)

### Request/Response Logging
Every API request logs:

```json
{
  "Timestamp": "2026-04-03T10:15:30.123Z",
  "Level": "Information",
  "MessageTemplate": "API Request Started {@RequestDetails}",
  "RequestDetails": {
    "RequestId": "0HN4URVT7I8MS:00000001",
    "Method": "POST",
    "Path": "/api/user/login",
    "QueryString": "",
    "RemoteIP": "127.0.0.1",
    "Timestamp": "2026-04-03T10:15:30.123Z"
  }
}
```

Request completion logs:

```json
{
  "Timestamp": "2026-04-03T10:15:30.234Z",
  "Level": "Information",
  "MessageTemplate": "API Request Completed {@RequestCompletionDetails}",
  "RequestCompletionDetails": {
    "RequestId": "0HN4URVT7I8MS:00000001",
    "Method": "POST",
    "Path": "/api/user/login",
    "StatusCode": 200,
    "ElapsedMilliseconds": 111,
    "IsSuccess": true,
    "Timestamp": "2026-04-03T10:15:30.234Z"
  }
}
```

## Viewing Logs

### Console
Logs appear in real-time in the application console with timestamps and levels.

### SEQ Dashboard
1. Open [http://localhost:5341](http://localhost:5341)
2. Use the search tab to filter logs by:
   - `Path = "/api/ai-checks"`
   - `StatusCode = 200`
   - `ElapsedMilliseconds > 500`
   - `RequestId = "specific-id"`

## Disabling SEQ

If you don't want to use SEQ locally, you can:

1. Keep only the Console sink in `appsettings.json`:
```json
"WriteTo": [
  {
    "Name": "Console"
  }
]
```

2. Or set SEQ to a disabled level:
```json
"Serilog": {
  "MinimumLevel": "Error"  // Only log errors and above
}
```

## Production Deployment

For production:

1. Update SEQ URL to your hosted SEQ server
2. Use API key authentication if enabled
3. Adjust log levels (Warning or Error for performance)
4. Consider log retention policies in SEQ

```json
"WriteTo": [
  {
    "Name": "Seq",
    "Args": {
      "serverUrl": "https://your-seq-server.com",
      "apiKey": "your-api-key-here",
      "restrictedToMinimumLevel": "Warning"
    }
  }
]
```

## Troubleshooting

### SEQ Connection Failed
- Ensure SEQ is running at `http://localhost:5341`
- Check firewall/network connectivity
- Logs will still work with console sink if SEQ is unavailable

### Missing Logs
- Check log level in `appsettings.json`
- Entity Framework queries need `"Microsoft.EntityFrameworkCore": "Debug"` or lower
- Request details only log for API calls, not health checks

### Performance
- Request logging is minimal overhead (single async operation)
- SEQ batches logs for efficient transmission
- Adjust `bulkPostingLimit` in Serilog config if needed

## Log Enrichment

The following context is automatically added to all logs:

- `MachineName`: Server hostname
- `ThreadId`: Thread identifier  
- `Application`: "WebChecker.Server"
- `Environment`: "Development" or "Production"
- All custom properties from log context (user ID, request ID, etc.)
