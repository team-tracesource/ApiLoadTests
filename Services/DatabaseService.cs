using MongoDB.Bson;
using MongoDB.Driver;

namespace TraceSource.LoadTests.Services;

public class DatabaseService : IDisposable
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;

    public DatabaseService(string connectionString, string databaseName)
    {
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);
    }

    public async Task<string?> GetVerificationTokenAsync(string email, CancellationToken ct = default)
    {
        var users = _database.GetCollection<BsonDocument>("users");

        var filter = Builders<BsonDocument>.Filter.Eq("email", email);
        var user = await users.Find(filter).FirstOrDefaultAsync(ct);

        if (user == null) return null;

        var verifications = user.GetValue("verifications", new BsonArray()).AsBsonArray;
        foreach (var verification in verifications)
        {
            var doc = verification.AsBsonDocument;
            var type = doc.GetValue("type", "").AsString;
            if (type == "email-verification")
            {
                var token = doc.GetValue("token", "").AsString;
                // Token format is "email-verification:XXXXXX", we need just the code
                if (token.StartsWith("email-verification:"))
                {
                    return token.Replace("email-verification:", "");
                }
                return token;
            }
        }

        return null;
    }

    public async Task CleanupTestUserAsync(string email, CancellationToken ct = default)
    {
        // Clean up user
        var users = _database.GetCollection<BsonDocument>("users");
        var userFilter = Builders<BsonDocument>.Filter.Eq("email", email);
        var user = await users.Find(userFilter).FirstOrDefaultAsync(ct);

        if (user != null)
        {
            var userId = user.GetValue("_id").AsObjectId.ToString();

            // Clean up forms
            var forms = _database.GetCollection<BsonDocument>("forms");
            var formsFilter = Builders<BsonDocument>.Filter.Eq("creatorId", userId);
            await forms.DeleteManyAsync(formsFilter, ct);

            // Clean up organizations created by user
            var organizations = _database.GetCollection<BsonDocument>("organizations");
            var orgFilter = Builders<BsonDocument>.Filter.Eq("creatorId", userId);
            await organizations.DeleteManyAsync(orgFilter, ct);

            // Clean up apps created by user
            var apps = _database.GetCollection<BsonDocument>("apps");
            var appFilter = Builders<BsonDocument>.Filter.Eq("creatorId", userId);
            await apps.DeleteManyAsync(appFilter, ct);

            // Delete user
            await users.DeleteOneAsync(userFilter, ct);
        }
    }

    public async Task CleanupAllTestUsersAsync(string emailPattern, CancellationToken ct = default)
    {
        Console.WriteLine($"Cleaning up test users matching pattern: {emailPattern}");

        var users = _database.GetCollection<BsonDocument>("users");
        var filter = Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression(emailPattern));
        var testUsers = await users.Find(filter).ToListAsync(ct);

        foreach (var user in testUsers)
        {
            var email = user.GetValue("email").AsString;
            await CleanupTestUserAsync(email, ct);
        }

        Console.WriteLine($"Cleaned up {testUsers.Count} test users");
    }

    public void Dispose()
    {
        // MongoClient doesn't require explicit disposal
    }
}
