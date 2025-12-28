using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TraceSource.LoadTests.Metrics;
using TraceSource.LoadTests.Models;

namespace TraceSource.LoadTests.Services;

public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MetricsCollector _metrics;
    private readonly string _currentPhase;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(string baseUrl, MetricsCollector metrics, string currentPhase)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _metrics = metrics;
        _currentPhase = currentPhase;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );
    }

    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<(AuthResponse? Response, bool IsSuccess, string? Error)> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<AuthResponse>(
            "POST",
            "/api/v1/auth/register",
            request,
            ct
        );
    }

    public async Task<(AuthResponse? Response, bool IsSuccess, string? Error)> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<AuthResponse>("POST", "/api/v1/auth/login", request, ct);
    }

    public async Task<(object? Response, bool IsSuccess, string? Error)> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<object>("POST", "/api/v1/auth/email/verify", request, ct);
    }

    public async Task<(
        OnboardingResponse? Response,
        bool IsSuccess,
        string? Error
    )> AutoOnboardAsync(CancellationToken ct = default)
    {
        return await ExecuteRequestAsync<OnboardingResponse>(
            "POST",
            "/api/v1/onboarding/auto",
            null,
            ct
        );
    }

    public async Task<(FormResponse? Response, bool IsSuccess, string? Error)> CreateFormAsync(
        CreateFormRequest request,
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<FormResponse>("POST", "/api/v1/forms", request, ct);
    }

    public async Task<(FormsResponse? Response, bool IsSuccess, string? Error)> GetFormsAsync(
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<FormsResponse>("GET", "/api/v1/forms", null, ct);
    }

    public async Task<(FormResponse? Response, bool IsSuccess, string? Error)> GetFormAsync(
        string formId,
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<FormResponse>("GET", $"/api/v1/forms/{formId}", null, ct);
    }

    public async Task<(object? Response, bool IsSuccess, string? Error)> GetFormStatsAsync(
        CancellationToken ct = default
    )
    {
        return await ExecuteRequestAsync<object>("GET", "/api/v1/forms/stats", null, ct);
    }

    private async Task<(T? Response, bool IsSuccess, string? Error)> ExecuteRequestAsync<T>(
        string method,
        string endpoint,
        object? body,
        CancellationToken ct
    )
    {
        var stopwatch = Stopwatch.StartNew();
        int statusCode = 0;
        bool isSuccess = false;
        string? errorMessage = null;
        T? response = default;

        try
        {
            HttpResponseMessage httpResponse;

            if (method == "GET")
            {
                httpResponse = await _httpClient.GetAsync(endpoint, ct);
            }
            else if (method == "POST")
            {
                httpResponse =
                    body != null
                        ? await _httpClient.PostAsJsonAsync(endpoint, body, _jsonOptions, ct)
                        : await _httpClient.PostAsync(endpoint, null, ct);
            }
            else
            {
                throw new NotSupportedException($"HTTP method {method} not supported");
            }

            stopwatch.Stop();
            statusCode = (int)httpResponse.StatusCode;
            isSuccess = httpResponse.IsSuccessStatusCode;

            if (isSuccess)
            {
                var content = await httpResponse.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrEmpty(content))
                {
                    response = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }
            }
            else
            {
                var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    errorMessage = $"HTTP {statusCode} {httpResponse.ReasonPhrase}";
                }
                else
                {
                    if (responseBody.Length > 300)
                    {
                        responseBody = responseBody[..300] + "...";
                    }
                    errorMessage = $"HTTP {statusCode}: {responseBody}";
                }
            }
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            errorMessage = "Request timeout";
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            errorMessage = $"Network error: {ex.Message}";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errorMessage = $"Error: {ex.Message}";
        }

        _metrics.RecordRequest(
            _currentPhase,
            endpoint,
            method,
            statusCode,
            stopwatch.ElapsedMilliseconds,
            isSuccess,
            errorMessage
        );

        return (response, isSuccess, errorMessage);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
