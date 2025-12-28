namespace TraceSource.LoadTests.Models;

public record TestUser
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? OrganizationId { get; set; }
}

public record RegisterRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public record LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public record VerifyEmailRequest
{
    public required string Email { get; init; }
    public required string Token { get; init; }
}

public record CreateFormRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<string> Tags { get; init; } = [];
    public required string Status { get; init; }
}

public record AuthResponse
{
    public TokenPair? Token { get; init; }
    public UserDto? User { get; init; }
}

public record TokenPair
{
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
}

public record UserDto
{
    public string? Id { get; init; }
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

public record OnboardingResponse
{
    public OrganizationSummary? Organization { get; init; }
    public AppSummary? App { get; init; }
}

public record OrganizationSummary
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public record AppSummary
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
}

public record FormResponse
{
    public FormDto? Form { get; init; }
}

public record FormDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Status { get; init; }
}

public record FormsResponse
{
    public PagedResult<FormSummary>? Forms { get; init; }
}

public record FormSummary
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Status { get; init; }
}

public record PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
