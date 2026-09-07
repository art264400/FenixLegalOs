using System.Security.Cryptography;
using Dapper;
using FenixLegalOs.Models;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public static class PasswordHelper
{
    public static (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        string salt = Convert.ToHexString(saltBytes);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32
        );
        string hash = Convert.ToHexString(hashBytes);
        return (hash, salt);
    }

    public static bool VerifyPassword(string password, string hash, string salt)
    {
        try
        {
            byte[] saltBytes = Convert.FromHexString(salt);
            byte[] expectedHashBytes = Convert.FromHexString(hash);
            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                iterations: 100_000,
                HashAlgorithmName.SHA256,
                outputLength: 32
            );
            return CryptographicOperations.FixedTimeEquals(expectedHashBytes, actualHashBytes);
        }
        catch
        {
            return false;
        }
    }
}

public class UserRepository
{
    private readonly DbInitializer _db;

    public UserRepository(DbInitializer db)
    {
        _db = db;
    }

    private SqliteConnection GetConn()
    {
        var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        return conn;
    }

    public UserAccount? GetUserByEmail(string email)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<UserAccount>(@"
            SELECT id AS Id, email AS Email, password_hash AS PasswordHash, salt AS Salt,
                   name AS Name, company AS Company, position AS Position, messenger AS Messenger,
                   terms_accepted AS TermsAccepted, terms_accepted_at AS TermsAcceptedAt,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE LOWER(email) = LOWER(@email)
        ", new { email = email.Trim() });
    }

    public UserAccount? GetUserById(string id)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<UserAccount>(@"
            SELECT id AS Id, email AS Email, password_hash AS PasswordHash, salt AS Salt,
                   name AS Name, company AS Company, position AS Position, messenger AS Messenger,
                   terms_accepted AS TermsAccepted, terms_accepted_at AS TermsAcceptedAt,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE id = @id
        ", new { id });
    }

    public UserAccount CreateUser(string email, string password, string name, string company, string position, string? messenger)
    {
        using var conn = GetConn();
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        var (hash, salt) = PasswordHelper.HashPassword(password);

        conn.Execute(@"
            INSERT INTO users (id, email, password_hash, salt, name, company, position, messenger, terms_accepted, terms_accepted_at, created_at, updated_at)
            VALUES (@id, LOWER(@email), @hash, @salt, @name, @company, @position, @messenger, 1, @now, @now, @now)
        ", new
        {
            id,
            email = email.Trim(),
            hash,
            salt,
            name = name.Trim(),
            company = company.Trim(),
            position = position.Trim(),
            messenger = string.IsNullOrWhiteSpace(messenger) ? null : messenger.Trim(),
            now
        });

        return new UserAccount
        {
            Id = id,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = hash,
            Salt = salt,
            Name = name.Trim(),
            Company = company.Trim(),
            Position = position.Trim(),
            Messenger = string.IsNullOrWhiteSpace(messenger) ? null : messenger.Trim(),
            TermsAccepted = true,
            TermsAcceptedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public UserAccount? VerifyLogin(string email, string password)
    {
        var user = GetUserByEmail(email);
        if (user == null) return null;

        if (PasswordHelper.VerifyPassword(password, user.PasswordHash, user.Salt))
        {
            return user;
        }

        return null;
    }

    public bool AttachUserToSession(string sessionId, string userId)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        int rows = conn.Execute(@"
            UPDATE sessions
            SET user_id = @userId, terms_accepted = 1, terms_accepted_at = @now
            WHERE id = @sessionId AND (user_id IS NULL OR user_id = '' OR user_id = @userId)
        ", new { sessionId, userId, now });
        return rows > 0;
    }

    public string CreateSessionToken(string userId, int validDays = 30)
    {
        using var conn = GetConn();
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var createdAt = now.ToString("o");
        var expiresAt = now.AddDays(validDays).ToString("o");

        conn.Execute(@"
            INSERT INTO user_tokens (token, user_id, created_at, expires_at)
            VALUES (@token, @userId, @createdAt, @expiresAt)
        ", new { token, userId, createdAt, expiresAt });

        return token;
    }

    public UserAccount? GetUserByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        var user = conn.QuerySingleOrDefault<UserAccount>(@"
            SELECT u.id AS Id, u.email AS Email, u.password_hash AS PasswordHash, u.salt AS Salt,
                   u.name AS Name, u.company AS Company, u.position AS Position, u.messenger AS Messenger,
                   u.terms_accepted AS TermsAccepted, u.terms_accepted_at AS TermsAcceptedAt,
                   u.created_at AS CreatedAt, u.updated_at AS UpdatedAt
            FROM users u
            INNER JOIN user_tokens t ON u.id = t.user_id
            WHERE t.token = @token AND t.expires_at > @now
        ", new { token, now });

        return user;
    }
}
