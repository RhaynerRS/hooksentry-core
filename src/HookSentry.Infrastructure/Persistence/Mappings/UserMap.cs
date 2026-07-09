using FluentNHibernate.Mapping;
using HookSentry.Domain.Users;

namespace HookSentry.Infrastructure.Persistence.Mappings;

public class UserMap : ClassMap<User>
{
    public UserMap()
    {
        Table("users");
        Not.LazyLoad();

        Id(x => x.Id, "id").GeneratedBy.Assigned();

        Map(x => x.TenantId, "tenant_id").Not.Nullable();
        Map(x => x.Email, "email").Length(255).Not.Nullable();
        Map(x => x.PasswordHash, "password_hash").Length(512).Nullable();
        Map(x => x.Status, "status").Not.Nullable().CustomType<UserStatus>();
        Map(x => x.Role, "role").Not.Nullable().CustomType<UserRole>();
        Map(x => x.CreatedAt, "created_at").Not.Nullable();
        Map(x => x.UpdatedAt, "updated_at").Not.Nullable();
    }
}
