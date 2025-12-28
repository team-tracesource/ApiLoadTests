using TraceSource.LoadTests.Metrics;
using TraceSource.LoadTests.Models;
using TraceSource.LoadTests.Services;

namespace TraceSource.LoadTests;

public class UserScenario(
    string baseUrl,
    MetricsCollector metrics,
    DatabaseService database,
    string phase,
    int iterationsPerPhase
)
{
    private readonly Random _random = new();

    public async Task RunAsync(int userId, CancellationToken ct)
    {
        for (int iteration = 0; iteration < iterationsPerPhase && !ct.IsCancellationRequested; iteration++)
        {
            // Generate a fresh user for each iteration with unique email
            var testUser = GenerateTestUser(userId, iteration);

            Console.WriteLine(
                $"  [User {userId}] Starting iteration {iteration + 1}/{iterationsPerPhase}"
            );

            try
            {
                await RunSingleIterationAsync(testUser, ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"  [User {userId}] Iteration cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [User {userId}] Iteration {iteration + 1} failed: {ex.Message}");
            }

            // Cleanup user after each iteration
            try
            {
                await database.CleanupTestUserAsync(testUser.Email, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [User {userId}] Cleanup failed: {ex.Message}");
            }

            // Small delay between iterations
            await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 3)), ct);
        }
    }

    private async Task RunSingleIterationAsync(TestUser testUser, CancellationToken ct)
    {
        using var apiClient = new ApiClient(baseUrl, metrics, phase);

        // Step 1: Register
        var (registerResponse, registerSuccess, registerError) = await apiClient.RegisterAsync(
            new RegisterRequest
            {
                FirstName = testUser.FirstName,
                LastName = testUser.LastName,
                Email = testUser.Email,
                Password = testUser.Password,
            },
            ct
        );

        if (!registerSuccess || registerResponse?.Token?.AccessToken == null)
        {
            if (!registerSuccess)
            {
                Console.WriteLine($"    [User] Register failed: {registerError}");
            }

            // Try login if registration fails (user may exist from previous run)
            var (loginResponse, loginSuccess, loginError) = await apiClient.LoginAsync(
                new LoginRequest { Email = testUser.Email, Password = testUser.Password },
                ct
            );

            if (!loginSuccess || loginResponse?.Token?.AccessToken == null)
            {
                Console.WriteLine($"    [User] Login failed: {loginError}");
                Console.WriteLine($"    [User] Both register and login failed for {testUser.Email}");
                return;
            }

            testUser.AccessToken = loginResponse.Token.AccessToken;
            testUser.RefreshToken = loginResponse.Token.RefreshToken;
        }
        else
        {
            testUser.AccessToken = registerResponse.Token.AccessToken;
            testUser.RefreshToken = registerResponse.Token.RefreshToken;
        }

        apiClient.SetAuthToken(testUser.AccessToken!);

        // Step 2: Verify Email (through DB)
        await Task.Delay(500, ct); // Small delay for token to be saved
        var verificationToken = await database.GetVerificationTokenAsync(testUser.Email, ct);
        if (!string.IsNullOrEmpty(verificationToken))
        {
            await apiClient.VerifyEmailAsync(
                new VerifyEmailRequest { Email = testUser.Email, Token = verificationToken },
                ct
            );
        }

        // Step 3: Onboarding
        var (onboardingResponse, onboardingSuccess, _) = await apiClient.AutoOnboardAsync(ct);
        if (onboardingSuccess && onboardingResponse?.Organization?.Id != null)
        {
            testUser.OrganizationId = onboardingResponse.Organization.Id;
        }

        // Step 4: Create Forms
        var createdFormIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var (formResponse, formSuccess, _) = await apiClient.CreateFormAsync(
                new CreateFormRequest
                {
                    Name = $"Test Form {i + 1} - {DateTime.UtcNow:HHmmss}",
                    Description = $"Load test form created at {DateTime.UtcNow}",
                    Tags = ["load-test", "automated"],
                    Status = "drafted",
                },
                ct
            );

            if (formSuccess && formResponse?.Form?.Id != null)
            {
                createdFormIds.Add(formResponse.Form.Id);
            }

            await Task.Delay(_random.Next(100, 300), ct);
        }

        // Step 5: Parallel batch - Organization calls (5x), Forms list calls (6x)
        var parallelTasks = new List<Task>();

        // Organization endpoint calls (5 times)
        if (!string.IsNullOrEmpty(testUser.OrganizationId))
        {
            for (int i = 0; i < 5; i++)
            {
                parallelTasks.Add(apiClient.GetOrganizationAsync(testUser.OrganizationId, ct));
            }
        }

        // All forms endpoint calls (6 times)
        for (int i = 0; i < 6; i++)
        {
            parallelTasks.Add(apiClient.GetFormsAsync(ct));
        }

        // Execute organization and forms list calls in parallel
        await Task.WhenAll(parallelTasks);
        await Task.Delay(_random.Next(50, 150), ct);

        // Step 6: Form details calls (6 times) - parallelize if we have enough forms
        var formDetailTasks = new List<Task>();
        for (int i = 0; i < 6; i++)
        {
            // Cycle through created form IDs
            var formId = createdFormIds.Count > 0 
                ? createdFormIds[i % createdFormIds.Count] 
                : null;
            
            if (!string.IsNullOrEmpty(formId))
            {
                formDetailTasks.Add(apiClient.GetFormAsync(formId, ct));
            }
        }

        if (formDetailTasks.Count > 0)
        {
            await Task.WhenAll(formDetailTasks);
        }
        await Task.Delay(_random.Next(50, 150), ct);

        // Step 7: Get form stats
        await apiClient.GetFormStatsAsync(ct);

        // Step 8: Logout (clear token)
        apiClient.ClearAuthToken();
    }

    private TestUser GenerateTestUser(int userId, int iteration)
    {
        var timestamp = DateTime.UtcNow.Ticks;
        var email = $"test+loadtest.u{userId}.i{iteration}.{timestamp}@yopmail.com";

        return new TestUser
        {
            Id = string.Empty,
            Email = email,
            Password = "LoadTest@12345",
            FirstName = $"LoadUser{userId}",
            LastName = $"Test{timestamp % 10000}",
        };
    }
}
