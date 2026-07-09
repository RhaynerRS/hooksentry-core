namespace HookSentry.Domain.Users;

public class User
{
    public virtual Guid Id { get; protected set; }
    public virtual Guid TenantId { get; protected set; }
    public virtual string Email { get; protected set; } = default!;
    public virtual string? PasswordHash { get; protected set; }
    public virtual UserStatus Status { get; protected set; }
    public virtual UserRole Role { get; protected set; }
    public virtual DateTimeOffset CreatedAt { get; protected set; }
    public virtual DateTimeOffset UpdatedAt { get; protected set; }

    protected User() { }

    public User(Guid tenantId, string email, string passwordHash, UserRole role = UserRole.Developer)
    {
        SetTenantId(tenantId);
        SetEmail(email);
        SetPasswordHash(passwordHash);
        SetRole(role);

        Id = Guid.NewGuid();
        Status = UserStatus.Active;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    private User(Guid tenantId, string email, UserRole role)
    {
        SetTenantId(tenantId);
        SetEmail(email);
        SetRole(role);

        Id = Guid.NewGuid();
        Status = UserStatus.Active;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }


    public static User CreateExternal(Guid tenantId, string email, UserRole role = UserRole.Developer)
        => new(tenantId, email, role);

    public virtual bool IsExternalOnly => PasswordHash is null;

    private void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    public virtual void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        if (email.Length > 255)
            throw new ArgumentException("Email cannot exceed 255 characters.", nameof(email));
        if (!email.Contains('@'))
            throw new ArgumentException("Email must be a valid address.", nameof(email));
        Email = email.Trim().ToLowerInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash cannot be null or empty.", nameof(passwordHash));
        PasswordHash = passwordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void SetRole(UserRole role)
    {
        if (!Enum.IsDefined<UserRole>(role))
            throw new ArgumentOutOfRangeException(nameof(role), "Invalid role.");
        Role = role;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void Activate()
    {
        Status = UserStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public virtual void Deactivate()
    {
        Status = UserStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
