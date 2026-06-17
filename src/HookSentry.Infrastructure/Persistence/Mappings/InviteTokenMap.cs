using FluentNHibernate.Mapping;
using HookSentry.Domain.Invites;

namespace HookSentry.Infrastructure.Persistence.Mappings;

public class InviteTokenMap : ClassMap<InviteToken>
{
    public InviteTokenMap()
    {
        Table("invite_tokens");
        Not.LazyLoad();
        OptimisticLock.Version();

        Id(x => x.Id, "id").GeneratedBy.Assigned();
        Version(x => x.Version).Column("version");
        Map(x => x.TenantId, "tenant_id").Not.Nullable();
        Map(x => x.Token, "token").Not.Nullable().Length(64);
        Map(x => x.ExpiresAt, "expires_at").Not.Nullable();
        Map(x => x.UsedAt, "used_at").Nullable();
        Map(x => x.Status, "status").Not.Nullable().CustomType<InviteTokenStatus>();
        Map(x => x.CreatedAt, "created_at").Not.Nullable();
        Map(x => x.UpdatedAt, "updated_at").Not.Nullable();
    }
}
