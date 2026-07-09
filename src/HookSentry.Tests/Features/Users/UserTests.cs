using HookSentry.Domain.Users;

namespace HookSentry.Tests.Features.Users;

public class UserTests
{
    private static readonly Guid ValidTenantId = Guid.NewGuid();
    private const string ValidEmail = "john@example.com";
    private const string ValidHash = "salt123:hash456";

    public class Constructor
    {
        [Fact]
        public void Should_Generate_Non_Empty_Id()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.NotEqual(Guid.Empty, user.Id);
        }

        [Fact]
        public void Two_Users_Should_Have_Different_Ids()
        {
            var a = new User(ValidTenantId, "a@example.com", ValidHash);
            var b = new User(ValidTenantId, "b@example.com", ValidHash);

            Assert.NotEqual(a.Id, b.Id);
        }

        [Fact]
        public void Should_Assign_Provided_TenantId()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Equal(ValidTenantId, user.TenantId);
        }

        [Fact]
        public void Should_Normalize_Email_To_Lowercase_On_Creation()
        {
            var user = new User(ValidTenantId, "John@EXAMPLE.COM", ValidHash);

            Assert.Equal("john@example.com", user.Email);
        }

        [Fact]
        public void Should_Assign_Provided_PasswordHash()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Equal(ValidHash, user.PasswordHash);
        }

        [Fact]
        public void Role_Should_Default_To_Developer()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Equal(UserRole.Developer, user.Role);
        }

        [Fact]
        public void Should_Accept_Admin_Role()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash, UserRole.Admin);

            Assert.Equal(UserRole.Admin, user.Role);
        }

        [Fact]
        public void Status_Should_Be_Active_On_Creation()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Equal(UserStatus.Active, user.Status);
        }

        [Fact]
        public void CreatedAt_Should_Be_Set_To_UtcNow()
        {
            var before = DateTimeOffset.UtcNow;
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.True(user.CreatedAt >= before);
        }

        [Fact]
        public void UpdatedAt_Should_Be_Set_To_UtcNow()
        {
            var before = DateTimeOffset.UtcNow;
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.True(user.UpdatedAt >= before);
        }

        [Fact]
        public void CreatedAt_And_UpdatedAt_Should_Be_Equal_On_Creation()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Equal(user.CreatedAt, user.UpdatedAt);
        }

        [Fact]
        public void Should_Throw_When_TenantId_Is_Empty()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(Guid.Empty, ValidEmail, ValidHash));

            Assert.Contains("TenantId", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_Email_Is_Null()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, null!, ValidHash));

            Assert.Contains("Email", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_Email_Is_Empty()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, "", ValidHash));

            Assert.Contains("Email", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_Email_Is_Whitespace_Only()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, "   ", ValidHash));

            Assert.Contains("Email", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_Email_Does_Not_Contain_At_Sign()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, "emailwithoutatsign.com", ValidHash));

            Assert.Contains("Email", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_Email_Exceeds_255_Chars()
        {
            var longEmail = new string('a', 250) + "@x.com";

            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, longEmail, ValidHash));

            Assert.Contains("255", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_PasswordHash_Is_Null()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, ValidEmail, null!));

            Assert.Contains("PasswordHash", ex.Message);
        }

        [Fact]
        public void Should_Throw_When_PasswordHash_Is_Empty()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new User(ValidTenantId, ValidEmail, ""));

            Assert.Contains("PasswordHash", ex.Message);
        }
    }

    public class SetEmail
    {
        [Fact]
        public void Should_Update_Email()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.SetEmail("new@example.com");

            Assert.Equal("new@example.com", user.Email);
        }

        [Fact]
        public void Should_Normalize_Email_To_Lowercase()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.SetEmail("NEW@EXAMPLE.COM");

            Assert.Equal("new@example.com", user.Email);
        }

        [Fact]
        public void Should_Trim_Email_Whitespace()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.SetEmail("  new@example.com  ");

            Assert.Equal("new@example.com", user.Email);
        }

        [Fact]
        public void Should_Update_UpdatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            Thread.Sleep(20);
            var before = DateTimeOffset.UtcNow;

            user.SetEmail("new@example.com");

            Assert.True(user.UpdatedAt >= before);
        }

        [Fact]
        public void Should_Not_Change_CreatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var originalCreatedAt = user.CreatedAt;
            Thread.Sleep(20);

            user.SetEmail("new@example.com");

            Assert.Equal(originalCreatedAt, user.CreatedAt);
        }

        [Fact]
        public void Should_Throw_When_Email_Is_Null()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Throws<ArgumentException>(() => user.SetEmail(null!));
        }

        [Fact]
        public void Should_Throw_When_Email_Is_Empty()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Throws<ArgumentException>(() => user.SetEmail(""));
        }

        [Fact]
        public void Should_Throw_When_Email_Does_Not_Contain_At_Sign()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Throws<ArgumentException>(() => user.SetEmail("invalid.com"));
        }

        [Fact]
        public void Should_Throw_When_Email_Exceeds_255_Chars()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var longEmail = new string('a', 250) + "@x.com";

            Assert.Throws<ArgumentException>(() => user.SetEmail(longEmail));
        }
    }

    public class SetPasswordHash
    {
        [Fact]
        public void Should_Update_PasswordHash()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.SetPasswordHash("newSalt:newHash");

            Assert.Equal("newSalt:newHash", user.PasswordHash);
        }

        [Fact]
        public void Should_Update_UpdatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            Thread.Sleep(20);
            var before = DateTimeOffset.UtcNow;

            user.SetPasswordHash("newSalt:newHash");

            Assert.True(user.UpdatedAt >= before);
        }

        [Fact]
        public void Should_Not_Change_CreatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var originalCreatedAt = user.CreatedAt;
            Thread.Sleep(20);

            user.SetPasswordHash("newSalt:newHash");

            Assert.Equal(originalCreatedAt, user.CreatedAt);
        }

        [Fact]
        public void Should_Throw_When_PasswordHash_Is_Null()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Throws<ArgumentException>(() => user.SetPasswordHash(null!));
        }

        [Fact]
        public void Should_Throw_When_PasswordHash_Is_Empty()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Throws<ArgumentException>(() => user.SetPasswordHash(""));
        }

        [Fact]
        public void Should_Throw_When_PasswordHash_Is_Whitespace_Only()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.Throws<ArgumentException>(() => user.SetPasswordHash("   "));
        }
    }

    public class SetRole
    {
        [Fact]
        public void Should_Update_Role_To_Admin()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.SetRole(UserRole.Admin);

            Assert.Equal(UserRole.Admin, user.Role);
        }

        [Fact]
        public void Should_Keep_Developer_Role()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash, UserRole.Admin);

            user.SetRole(UserRole.Developer);

            Assert.Equal(UserRole.Developer, user.Role);
        }

        [Fact]
        public void Should_Update_UpdatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            Thread.Sleep(20);
            var before = DateTimeOffset.UtcNow;

            user.SetRole(UserRole.Admin);

            Assert.True(user.UpdatedAt >= before);
        }

        [Fact]
        public void Should_Not_Change_CreatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var originalCreatedAt = user.CreatedAt;
            Thread.Sleep(20);

            user.SetRole(UserRole.Admin);

            Assert.Equal(originalCreatedAt, user.CreatedAt);
        }

        [Fact]
        public void Should_Throw_When_Role_Is_Invalid()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var invalidRole = (UserRole)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => user.SetRole(invalidRole));
        }
    }

    public class Activate
    {
        [Fact]
        public void Should_Set_Status_To_Active()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            user.Deactivate();

            user.Activate();

            Assert.Equal(UserStatus.Active, user.Status);
        }

        [Fact]
        public void Should_Be_Idempotent_When_Already_Active()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.Activate();

            Assert.Equal(UserStatus.Active, user.Status);
        }

        [Fact]
        public void Should_Update_UpdatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            user.Deactivate();
            Thread.Sleep(20);
            var before = DateTimeOffset.UtcNow;

            user.Activate();

            Assert.True(user.UpdatedAt >= before);
        }

        [Fact]
        public void Should_Not_Change_CreatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var originalCreatedAt = user.CreatedAt;
            user.Deactivate();
            Thread.Sleep(20);

            user.Activate();

            Assert.Equal(originalCreatedAt, user.CreatedAt);
        }
    }

    public class Deactivate
    {
        [Fact]
        public void Should_Set_Status_To_Inactive()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            user.Deactivate();

            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Fact]
        public void Should_Be_Idempotent_When_Already_Inactive()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            user.Deactivate();

            user.Deactivate();

            Assert.Equal(UserStatus.Inactive, user.Status);
        }

        [Fact]
        public void Should_Update_UpdatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            Thread.Sleep(20);
            var before = DateTimeOffset.UtcNow;

            user.Deactivate();

            Assert.True(user.UpdatedAt >= before);
        }

        [Fact]
        public void Should_Not_Change_CreatedAt()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);
            var originalCreatedAt = user.CreatedAt;
            Thread.Sleep(20);

            user.Deactivate();

            Assert.Equal(originalCreatedAt, user.CreatedAt);
        }
    }

    public class CreateExternal
    {
        [Fact]
        public void Should_Create_Active_User_Without_Password()
        {
            var user = User.CreateExternal(ValidTenantId, ValidEmail, UserRole.Admin);

            Assert.NotEqual(Guid.Empty, user.Id);
            Assert.Equal(ValidTenantId, user.TenantId);
            Assert.Equal(ValidEmail, user.Email);
            Assert.Equal(UserRole.Admin, user.Role);
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.Null(user.PasswordHash);
            Assert.True(user.IsExternalOnly);
        }

        [Fact]
        public void Should_Default_To_Developer_Role()
        {
            var user = User.CreateExternal(ValidTenantId, ValidEmail);

            Assert.Equal(UserRole.Developer, user.Role);
        }

        [Fact]
        public void Should_Normalize_Email_To_Lowercase()
        {
            var user = User.CreateExternal(ValidTenantId, "John@EXAMPLE.COM");

            Assert.Equal("john@example.com", user.Email);
        }

        [Fact]
        public void Should_Throw_When_TenantId_Is_Empty()
        {
            Assert.Throws<ArgumentException>(() => User.CreateExternal(Guid.Empty, ValidEmail));
        }

        [Fact]
        public void Should_Throw_When_Email_Is_Invalid()
        {
            Assert.Throws<ArgumentException>(() => User.CreateExternal(ValidTenantId, "no-at-sign"));
        }

        [Fact]
        public void Password_User_Should_Not_Be_External_Only()
        {
            var user = new User(ValidTenantId, ValidEmail, ValidHash);

            Assert.False(user.IsExternalOnly);
        }
    }
}
