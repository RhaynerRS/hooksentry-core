using HookSentry.Domain.Events;

namespace HookSentry.Tests.Features.Events;

public class EventTests
{
    private static readonly Guid ValidTenantId = Guid.NewGuid();
    private static readonly Guid ValidDestinationUrlId = Guid.NewGuid();
    private const string ValidPayload = "{\"order_id\":\"123\",\"amount\":99.90}";

    public class Construtor
    {
        [Fact]
        public void Deve_Gerar_Id_Nao_Vazio()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.NotEqual(Guid.Empty, evento.Id);
        }

        [Fact]
        public void Dois_Eventos_Devem_Ter_Ids_Diferentes()
        {
            var a = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var b = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.NotEqual(a.Id, b.Id);
        }

        [Fact]
        public void Deve_Atribuir_TenantId_Informado()
        {
            var tenantId = Guid.NewGuid();
            var evento = new Event(tenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(tenantId, evento.TenantId);
        }

        [Fact]
        public void Deve_Atribuir_DestinationUrlId_Informado()
        {
            var destId = Guid.NewGuid();
            var evento = new Event(ValidTenantId, destId, ValidPayload);

            Assert.Equal(destId, evento.DestinationUrlId);
        }

        [Fact]
        public void Deve_Atribuir_Payload_Informado()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(ValidPayload, evento.Payload);
        }

        [Fact]
        public void Status_Deve_Ser_Pending_Por_Padrao()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(EventStatus.Pending, evento.Status);
        }

        [Fact]
        public void CurrentRetryCount_Deve_Ser_Zero_Por_Padrao()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(0, evento.CurrentRetryCount);
        }

        [Fact]
        public void IdempotencyKey_Deve_Ser_Nula_Por_Padrao()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Null(evento.IdempotencyKey);
        }

        [Fact]
        public void Deve_Atribuir_IdempotencyKey_Informada()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload, "chave-unica-123");

            Assert.Equal("chave-unica-123", evento.IdempotencyKey);
        }

        [Fact]
        public void AcceptedAt_Deve_Ser_Definido_Como_UtcNow()
        {
            var antes = DateTimeOffset.UtcNow;
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.True(evento.AcceptedAt >= antes);
        }

        [Fact]
        public void NextAttemptAt_Deve_Ser_Nulo_Por_Padrao()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Null(evento.NextAttemptAt);
        }

        [Fact]
        public void DeliveredAt_Deve_Ser_Nulo_Por_Padrao()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Null(evento.DeliveredAt);
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_TenantId_For_Vazio()
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(Guid.Empty, ValidDestinationUrlId, ValidPayload));
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_DestinationUrlId_For_Vazio()
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, Guid.Empty, ValidPayload));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deve_Lancar_Excecao_Quando_Payload_For_Nulo_Ou_Vazio(string? payload)
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, ValidDestinationUrlId, payload!));
        }

        [Theory]
        [InlineData("nao-e-json")]
        [InlineData("{chave sem aspas: 1}")]
        [InlineData("texto simples")]
        public void Deve_Lancar_Excecao_Quando_Payload_For_Json_Invalido(string payload)
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, ValidDestinationUrlId, payload));
        }

        [Fact]
        public void Deve_Lancar_Excecao_Quando_IdempotencyKey_Exceder_255_Caracteres()
        {
            var chave = new string('x', 256);

            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload, chave));
        }

        [Fact]
        public void Deve_Aceitar_IdempotencyKey_Com_255_Caracteres()
        {
            var chave = new string('x', 255);
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload, chave);

            Assert.Equal(chave, evento.IdempotencyKey);
        }

        [Fact]
        public void Deve_Aceitar_Payload_Json_Array()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, "[1,2,3]");

            Assert.Equal("[1,2,3]", evento.Payload);
        }
    }

    public class MetodoSetPayload
    {
        [Fact]
        public void Deve_Atualizar_Payload()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            const string novoPayload = "{\"novo\":true}";

            evento.SetPayload(novoPayload);

            Assert.Equal(novoPayload, evento.Payload);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deve_Lancar_Excecao_Quando_Payload_For_Nulo_Ou_Vazio(string? payload)
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Throws<ArgumentException>(() => evento.SetPayload(payload!));
        }

        [Theory]
        [InlineData("nao-e-json")]
        [InlineData("{chave sem aspas: 1}")]
        public void Deve_Lancar_Excecao_Quando_Payload_For_Json_Invalido(string payload)
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Throws<ArgumentException>(() => evento.SetPayload(payload));
        }

        [Fact]
        public void Nao_Deve_Alterar_TenantId()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            evento.SetPayload("{\"updated\":true}");

            Assert.Equal(ValidTenantId, evento.TenantId);
        }

        [Fact]
        public void Nao_Deve_Alterar_AcceptedAt()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var acceptedAtOriginal = evento.AcceptedAt;
            Thread.Sleep(20);

            evento.SetPayload("{\"updated\":true}");

            Assert.Equal(acceptedAtOriginal, evento.AcceptedAt);
        }
    }

    public class MetodoMarkSucceeded
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Deve_Definir_Status_Como_Succeeded_A_Partir_De_Estado_Ativo(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            evento.MarkSucceeded();

            Assert.Equal(EventStatus.Succeeded, evento.Status);
        }

        [Fact]
        public void Deve_Definir_DeliveredAt()
        {
            var antes = DateTimeOffset.UtcNow;
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            evento.MarkSucceeded();

            Assert.NotNull(evento.DeliveredAt);
            Assert.True(evento.DeliveredAt >= antes);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Deve_Lancar_Excecao_A_Partir_De_Estado_Terminal(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evento.MarkSucceeded());
        }
    }

    public class MetodoMarkWaitingRetry
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Deve_Definir_Status_Como_WaitingRetry_A_Partir_De_Estado_Ativo(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();
            var proxima = DateTimeOffset.UtcNow.AddMinutes(5);

            evento.MarkWaitingRetry(1, proxima);

            Assert.Equal(EventStatus.WaitingRetry, evento.Status);
            Assert.Equal(1, evento.CurrentRetryCount);
            Assert.Equal(proxima, evento.NextAttemptAt);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Deve_Lancar_Excecao_A_Partir_De_Estado_Terminal(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            Assert.Throws<InvalidOperationException>(
                () => evento.MarkWaitingRetry(1, DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    public class MetodoMarkCriticalFailure
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Deve_Definir_Status_Como_CriticalFailure_A_Partir_De_Estado_Ativo(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            evento.MarkCriticalFailure();

            Assert.Equal(EventStatus.CriticalFailure, evento.Status);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Deve_Lancar_Excecao_A_Partir_De_Estado_Terminal(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evento.MarkCriticalFailure());
        }
    }

    public class MetodoMarkAuthenticationFailed
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Deve_Definir_Status_Como_AuthenticationFailed_A_Partir_De_Estado_Ativo(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            evento.MarkAuthenticationFailed();

            Assert.Equal(EventStatus.AuthenticationFailed, evento.Status);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Deve_Lancar_Excecao_A_Partir_De_Estado_Terminal(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evento.MarkAuthenticationFailed());
        }
    }

    public class MetodoResetForReplay
    {
        [Fact]
        public void Deve_Resetar_CurrentRetryCount_Para_Zero()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.CriticalFailure).Build();

            evento.ResetForReplay();

            Assert.Equal(0, evento.CurrentRetryCount);
        }

        [Fact]
        public void Deve_Definir_Status_Como_Pending()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.CriticalFailure).Build();

            evento.ResetForReplay();

            Assert.Equal(EventStatus.Pending, evento.Status);
        }

        [Fact]
        public void Deve_Definir_NextAttemptAt_Como_Agora_Ou_Posterior()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.CriticalFailure).Build();
            var antes = DateTimeOffset.UtcNow;

            evento.ResetForReplay();

            Assert.NotNull(evento.NextAttemptAt);
            Assert.True(evento.NextAttemptAt >= antes);
        }

        [Fact]
        public void Nao_Deve_Alterar_AcceptedAt()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.CriticalFailure).Build();
            var acceptedAtOriginal = evento.AcceptedAt;
            Thread.Sleep(20);

            evento.ResetForReplay();

            Assert.Equal(acceptedAtOriginal, evento.AcceptedAt);
        }

        [Fact]
        public void Nao_Deve_Alterar_Id()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.CriticalFailure).Build();
            var idOriginal = evento.Id;

            evento.ResetForReplay();

            Assert.Equal(idOriginal, evento.Id);
        }

        [Fact]
        public void Nao_Deve_Alterar_TenantId()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.CriticalFailure).Build();

            evento.ResetForReplay();

            Assert.Equal(ValidTenantId, evento.TenantId);
        }

        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.WaitingRetry)]
        [InlineData(EventStatus.Cancelled)]
        public void Deve_Lancar_Excecao_Quando_Status_Nao_For_CriticalFailure(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evento.ResetForReplay());
        }
    }

    public class MetodoCancel
    {
        [Fact]
        public void Deve_Definir_Status_Como_Cancelled_Quando_Pending()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            evento.Cancel();

            Assert.Equal(EventStatus.Cancelled, evento.Status);
        }

        [Fact]
        public void Deve_Definir_Status_Como_Cancelled_Quando_WaitingRetry()
        {
            var evento = new EventBuilder().ComStatus(EventStatus.WaitingRetry).Build();

            evento.Cancel();

            Assert.Equal(EventStatus.Cancelled, evento.Status);
        }

        [Theory]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.Cancelled)]
        public void Deve_Lancar_Excecao_Quando_Status_For_Diferente_De_Pending_Ou_WaitingRetry(EventStatus status)
        {
            var evento = new EventBuilder().ComStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evento.Cancel());
        }

        [Fact]
        public void Nao_Deve_Alterar_AcceptedAt()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var acceptedAtOriginal = evento.AcceptedAt;
            Thread.Sleep(20);

            evento.Cancel();

            Assert.Equal(acceptedAtOriginal, evento.AcceptedAt);
        }
    }

    // Builder auxiliar para criar Event com status específico via reflexão
    private class EventBuilder
    {
        private EventStatus _status = EventStatus.Pending;

        public EventBuilder ComStatus(EventStatus status)
        {
            _status = status;
            return this;
        }

        public Event Build()
        {
            var evento = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var prop = typeof(Event).GetProperty(
                nameof(Event.Status),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            prop!.GetSetMethod(nonPublic: true)!.Invoke(evento, [_status]);
            return evento;
        }
    }
}
