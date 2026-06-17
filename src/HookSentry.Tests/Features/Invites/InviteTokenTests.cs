using HookSentry.Domain.Invites;

namespace HookSentry.Tests.Features.Invites;

public class InviteTokenTests
{
    private static readonly Guid ValidTenantId = Guid.NewGuid();

    public class Construtor
    {
        [Fact]
        public void Deve_Gerar_Id_Nao_Vazio()
        {
            var invite = new InviteToken(ValidTenantId);

            Assert.NotEqual(Guid.Empty, invite.Id);
        }

        [Fact]
        public void Dois_Invites_Devem_Ter_Ids_Diferentes()
        {
            var a = new InviteToken(ValidTenantId);
            var b = new InviteToken(ValidTenantId);

            Assert.NotEqual(a.Id, b.Id);
        }

        [Fact]
        public void Deve_Atribuir_TenantId_Informado()
        {
            var invite = new InviteToken(ValidTenantId);

            Assert.Equal(ValidTenantId, invite.TenantId);
        }

        [Fact]
        public void Deve_Gerar_Token_Nao_Vazio()
        {
            var invite = new InviteToken(ValidTenantId);

            Assert.False(string.IsNullOrWhiteSpace(invite.Token));
        }

        [Fact]
        public void Dois_Invites_Devem_Ter_Tokens_Diferentes()
        {
            var a = new InviteToken(ValidTenantId);
            var b = new InviteToken(ValidTenantId);

            Assert.NotEqual(a.Token, b.Token);
        }

        [Fact]
        public void Status_Deve_Ser_Pending_Na_Criacao()
        {
            var invite = new InviteToken(ValidTenantId);

            Assert.Equal(InviteTokenStatus.Pending, invite.Status);
        }

        [Fact]
        public void UsedAt_Deve_Ser_Nulo_Na_Criacao()
        {
            var invite = new InviteToken(ValidTenantId);

            Assert.Null(invite.UsedAt);
        }

        [Fact]
        public void Deve_Definir_ExpiresAt_Com_Validade_Padrao_De_7_Dias()
        {
            var antes = DateTimeOffset.UtcNow;
            var invite = new InviteToken(ValidTenantId);

            Assert.True(invite.ExpiresAt >= antes.AddDays(7));
            Assert.True(invite.ExpiresAt <= DateTimeOffset.UtcNow.AddDays(7).AddSeconds(5));
        }

        [Fact]
        public void Deve_Definir_ExpiresAt_Com_ValidityDays_Informado()
        {
            var antes = DateTimeOffset.UtcNow;
            var invite = new InviteToken(ValidTenantId, validityDays: 14);

            Assert.True(invite.ExpiresAt >= antes.AddDays(14));
            Assert.True(invite.ExpiresAt <= DateTimeOffset.UtcNow.AddDays(14).AddSeconds(5));
        }

        [Fact]
        public void CreatedAt_E_UpdatedAt_Devem_Ser_Iguais_Na_Criacao()
        {
            var invite = new InviteToken(ValidTenantId);

            Assert.Equal(invite.CreatedAt, invite.UpdatedAt);
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_TenantId_For_Vazio()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new InviteToken(Guid.Empty));

            Assert.Contains("TenantId", ex.Message);
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_ValidityDays_For_Menor_Que_1()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new InviteToken(ValidTenantId, validityDays: 0));
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_ValidityDays_For_Maior_Que_30()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new InviteToken(ValidTenantId, validityDays: 31));
        }
    }

    public class MetodoUse
    {
        [Fact]
        public void Deve_Definir_Status_Como_Used()
        {
            var invite = new InviteToken(ValidTenantId);

            invite.Use();

            Assert.Equal(InviteTokenStatus.Used, invite.Status);
        }

        [Fact]
        public void Deve_Definir_UsedAt()
        {
            var antes = DateTimeOffset.UtcNow;
            var invite = new InviteToken(ValidTenantId);

            invite.Use();

            Assert.NotNull(invite.UsedAt);
            Assert.True(invite.UsedAt >= antes);
        }

        [Fact]
        public void Deve_Atualizar_UpdatedAt()
        {
            var invite = new InviteToken(ValidTenantId);
            Thread.Sleep(20);
            var antes = DateTimeOffset.UtcNow;

            invite.Use();

            Assert.True(invite.UpdatedAt >= antes);
        }

        [Fact]
        public void Nao_Deve_Alterar_CreatedAt()
        {
            var invite = new InviteToken(ValidTenantId);
            var createdAtOriginal = invite.CreatedAt;
            Thread.Sleep(20);

            invite.Use();

            Assert.Equal(createdAtOriginal, invite.CreatedAt);
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_Ja_Utilizado()
        {
            var invite = new InviteToken(ValidTenantId);
            invite.Use();

            var ex = Assert.Throws<InvalidOperationException>(() => invite.Use());

            Assert.Contains("already been used", ex.Message);
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_Expirado()
        {
            var invite = new InviteToken(ValidTenantId, validityDays: 1);
            typeof(InviteToken)
                .GetProperty(nameof(InviteToken.ExpiresAt))!
                .SetValue(invite, DateTimeOffset.UtcNow.AddDays(-1));

            var ex = Assert.Throws<InvalidOperationException>(() => invite.Use());

            Assert.Contains("expired", ex.Message);
        }
    }
}
