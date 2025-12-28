# TraceSource API Load Tests

A .NET 9 load testing tool for the TraceSource API.

## Overview

This load test simulates realistic user scenarios including:
- User registration
- Email verification (via direct DB access)
- Auto-onboarding
- Form creation
- Form reading (list and individual)
- Form stats retrieval
- Logout

## Test Phases

The test runs in 4 phases with increasing concurrent users:
1. **Phase 1**: 20 concurrent users for 10 minutes
2. **Phase 2**: 50 concurrent users for 10 minutes
3. **Phase 3**: 100 concurrent users for 10 minutes
4. **Phase 4**: 300 concurrent users for 10 minutes

After all phases, there's a 10-minute rest period.

Each user runs 10 iterations of the complete scenario per phase.

## Configuration

### Environment Variables

Create a `.env` file or set these environment variables:

```env
API_BASE_URL=http://localhost:5000
MONGODB_CONNECTION_STRING=mongodb://localhost:27017
MONGODB_DATABASE_NAME=TraceSourceDB
```

### appsettings.json

```json
{
  "Api": {
    "BaseUrl": "http://localhost:5000"
  },
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "TraceSourceDB"
  },
  "LoadTest": {
    "Phases": [
      { "Users": 20, "DurationMinutes": 10 },
      { "Users": 50, "DurationMinutes": 10 },
      { "Users": 100, "DurationMinutes": 10 },
      { "Users": 300, "DurationMinutes": 10 }
    ],
    "IterationsPerUser": 10,
    "RestDurationMinutes": 10
  }
}
```

## Running the Tests

```bash
cd LoadTests
dotnet restore
dotnet run
```

### Quick Test (Smaller Scale)

For a quick test run, modify `appsettings.json`:

```json
{
  "LoadTest": {
    "Phases": [
      { "Users": 5, "DurationMinutes": 2 },
      { "Users": 10, "DurationMinutes": 2 }
    ],
    "IterationsPerUser": 3,
    "RestDurationMinutes": 1
  }
}
```

## Metrics Collected

### Per Request
- Endpoint
- HTTP Method
- Status Code
- Latency (ms)
- Success/Failure
- Error Message (if failed)
- Timestamp

### Per Phase
- Total Requests
- Success/Failure counts
- Success Rate (%)
- Average Latency
- Min/Max Latency
- P50, P95, P99 Latency
- Requests per Second
- Breakdown by Endpoint

### Final Report
- Overall test duration
- Total requests across all phases
- Overall success rate
- Latency statistics
- Phase-by-phase comparison

## Output

### Console Output
Real-time progress and phase summaries are printed to the console.

### Report File
A detailed Markdown report is saved to:
```
load-test-report-{timestamp}.md
```

## Graceful Shutdown

Press `Ctrl+C` to gracefully stop the test. The tool will:
1. Stop all running scenarios
2. Clean up test users from the database
3. Print the final report with collected metrics

## Test User Cleanup

Test users are automatically cleaned up:
- After each user completes their iterations
- At the end of the test run
- On startup (cleans up from previous runs)

Test users are identified by their email pattern: `test+loadtest.user*@yopmail.com`

## Notes

- The API must be running before starting the load test
- MongoDB must be accessible for email verification bypass
- Test users are created and deleted during the test
- Forms and organizations created by test users are also cleaned up
