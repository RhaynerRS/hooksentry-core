using HookSentry.Domain.Events;

namespace HookSentry.Tests.Features.Events;

public class EventTests
{
    private static readonly Guid ValidTenantId = Guid.NewGuid();
    private static readonly Guid ValidDestinationUrlId = Guid.NewGuid();
    private const string ValidPayload = "{\"order_id\":\"123\",\"amount\":99.90}";

    public class Constructor
    {
        [Fact]
        public void Should_Generate_Non_Empty_Id()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.NotEqual(Guid.Empty, evt.Id);
        }

        [Fact]
        public void Two_Events_Should_Have_Different_Ids()
        {
            var a = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var b = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.NotEqual(a.Id, b.Id);
        }

        [Fact]
        public void Should_Assign_Provided_TenantId()
        {
            var tenantId = Guid.NewGuid();
            var evt = new Event(tenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(tenantId, evt.TenantId);
        }

        [Fact]
        public void Should_Assign_Provided_DestinationUrlId()
        {
            var destId = Guid.NewGuid();
            var evt = new Event(ValidTenantId, destId, ValidPayload);

            Assert.Equal(destId, evt.DestinationUrlId);
        }

        [Fact]
        public void Should_Assign_Provided_Payload()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(ValidPayload, evt.Payload);
        }

        [Fact]
        public void Status_Should_Default_To_Pending()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(EventStatus.Pending, evt.Status);
        }

        [Fact]
        public void CurrentRetryCount_Should_Default_To_Zero()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Equal(0, evt.CurrentRetryCount);
        }

        [Fact]
        public void IdempotencyKey_Should_Default_To_Null()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Null(evt.IdempotencyKey);
        }

        [Fact]
        public void Should_Assign_Provided_IdempotencyKey()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload, "unique-key-123");

            Assert.Equal("unique-key-123", evt.IdempotencyKey);
        }

        [Fact]
        public void AcceptedAt_Should_Be_Set_To_UtcNow()
        {
            var before = DateTimeOffset.UtcNow;
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.True(evt.AcceptedAt >= before);
        }

        [Fact]
        public void NextAttemptAt_Should_Default_To_Null()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Null(evt.NextAttemptAt);
        }

        [Fact]
        public void DeliveredAt_Should_Default_To_Null()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Null(evt.DeliveredAt);
        }

        [Fact]
        public void Should_Throw_When_TenantId_Is_Empty()
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(Guid.Empty, ValidDestinationUrlId, ValidPayload));
        }

        [Fact]
        public void Should_Throw_When_DestinationUrlId_Is_Empty()
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, Guid.Empty, ValidPayload));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_Throw_When_Payload_Is_Null_Or_Empty(string? payload)
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, ValidDestinationUrlId, payload!));
        }

        [Theory]
        [InlineData("not-json")]
        [InlineData("{key without quotes: 1}")]
        [InlineData("plain text")]
        public void Should_Throw_When_Payload_Is_Invalid_Json(string payload)
        {
            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, ValidDestinationUrlId, payload));
        }

        [Fact]
        public void Should_Throw_When_IdempotencyKey_Exceeds_255_Characters()
        {
            var key = new string('x', 256);

            Assert.Throws<ArgumentException>(() =>
                new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload, key));
        }

        [Fact]
        public void Should_Accept_IdempotencyKey_With_255_Characters()
        {
            var key = new string('x', 255);
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload, key);

            Assert.Equal(key, evt.IdempotencyKey);
        }

        [Fact]
        public void Should_Accept_Json_Array_Payload()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, "[1,2,3]");

            Assert.Equal("[1,2,3]", evt.Payload);
        }
    }

    public class SetPayload
    {
        [Fact]
        public void Should_Update_Payload()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            const string newPayload = "{\"new\":true}";

            evt.SetPayload(newPayload);

            Assert.Equal(newPayload, evt.Payload);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_Throw_When_Payload_Is_Null_Or_Empty(string? payload)
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Throws<ArgumentException>(() => evt.SetPayload(payload!));
        }

        [Theory]
        [InlineData("not-json")]
        [InlineData("{key without quotes: 1}")]
        public void Should_Throw_When_Payload_Is_Invalid_Json(string payload)
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            Assert.Throws<ArgumentException>(() => evt.SetPayload(payload));
        }

        [Fact]
        public void Should_Not_Change_TenantId()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            evt.SetPayload("{\"updated\":true}");

            Assert.Equal(ValidTenantId, evt.TenantId);
        }

        [Fact]
        public void Should_Not_Change_AcceptedAt()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var originalAcceptedAt = evt.AcceptedAt;
            Thread.Sleep(20);

            evt.SetPayload("{\"updated\":true}");

            Assert.Equal(originalAcceptedAt, evt.AcceptedAt);
        }
    }

    public class MarkSucceeded
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Should_Set_Status_To_Succeeded_From_Active_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            evt.MarkSucceeded();

            Assert.Equal(EventStatus.Succeeded, evt.Status);
        }

        [Fact]
        public void Should_Set_DeliveredAt()
        {
            var before = DateTimeOffset.UtcNow;
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            evt.MarkSucceeded();

            Assert.NotNull(evt.DeliveredAt);
            Assert.True(evt.DeliveredAt >= before);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Should_Throw_From_Terminal_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evt.MarkSucceeded());
        }
    }

    public class MarkWaitingRetry
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Should_Set_Status_To_WaitingRetry_From_Active_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();
            var next = DateTimeOffset.UtcNow.AddMinutes(5);

            evt.MarkWaitingRetry(1, next);

            Assert.Equal(EventStatus.WaitingRetry, evt.Status);
            Assert.Equal(1, evt.CurrentRetryCount);
            Assert.Equal(next, evt.NextAttemptAt);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Should_Throw_From_Terminal_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            Assert.Throws<InvalidOperationException>(
                () => evt.MarkWaitingRetry(1, DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    public class MarkCriticalFailure
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Should_Set_Status_To_CriticalFailure_From_Active_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            evt.MarkCriticalFailure();

            Assert.Equal(EventStatus.CriticalFailure, evt.Status);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Should_Throw_From_Terminal_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evt.MarkCriticalFailure());
        }
    }

    public class MarkAuthenticationFailed
    {
        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.WaitingRetry)]
        public void Should_Set_Status_To_AuthenticationFailed_From_Active_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            evt.MarkAuthenticationFailed();

            Assert.Equal(EventStatus.AuthenticationFailed, evt.Status);
        }

        [Theory]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.AuthenticationFailed)]
        [InlineData(EventStatus.Cancelled)]
        public void Should_Throw_From_Terminal_State(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evt.MarkAuthenticationFailed());
        }
    }

    public class ResetForReplay
    {
        [Fact]
        public void Should_Reset_CurrentRetryCount_To_Zero()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.CriticalFailure).Build();

            evt.ResetForReplay();

            Assert.Equal(0, evt.CurrentRetryCount);
        }

        [Fact]
        public void Should_Set_Status_To_Pending()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.CriticalFailure).Build();

            evt.ResetForReplay();

            Assert.Equal(EventStatus.Pending, evt.Status);
        }

        [Fact]
        public void Should_Set_NextAttemptAt_To_Now_Or_Later()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.CriticalFailure).Build();
            var before = DateTimeOffset.UtcNow;

            evt.ResetForReplay();

            Assert.NotNull(evt.NextAttemptAt);
            Assert.True(evt.NextAttemptAt >= before);
        }

        [Fact]
        public void Should_Not_Change_AcceptedAt()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.CriticalFailure).Build();
            var originalAcceptedAt = evt.AcceptedAt;
            Thread.Sleep(20);

            evt.ResetForReplay();

            Assert.Equal(originalAcceptedAt, evt.AcceptedAt);
        }

        [Fact]
        public void Should_Not_Change_Id()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.CriticalFailure).Build();
            var originalId = evt.Id;

            evt.ResetForReplay();

            Assert.Equal(originalId, evt.Id);
        }

        [Fact]
        public void Should_Not_Change_TenantId()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.CriticalFailure).Build();

            evt.ResetForReplay();

            Assert.Equal(ValidTenantId, evt.TenantId);
        }

        [Theory]
        [InlineData(EventStatus.Pending)]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.WaitingRetry)]
        [InlineData(EventStatus.Cancelled)]
        public void Should_Throw_When_Status_Is_Not_CriticalFailure(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evt.ResetForReplay());
        }
    }

    public class Cancel
    {
        [Fact]
        public void Should_Set_Status_To_Cancelled_When_Pending()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);

            evt.Cancel();

            Assert.Equal(EventStatus.Cancelled, evt.Status);
        }

        [Fact]
        public void Should_Set_Status_To_Cancelled_When_WaitingRetry()
        {
            var evt = new EventBuilder().WithStatus(EventStatus.WaitingRetry).Build();

            evt.Cancel();

            Assert.Equal(EventStatus.Cancelled, evt.Status);
        }

        [Theory]
        [InlineData(EventStatus.Processing)]
        [InlineData(EventStatus.Succeeded)]
        [InlineData(EventStatus.Failed)]
        [InlineData(EventStatus.CriticalFailure)]
        [InlineData(EventStatus.Cancelled)]
        public void Should_Throw_When_Status_Is_Not_Pending_Or_WaitingRetry(EventStatus status)
        {
            var evt = new EventBuilder().WithStatus(status).Build();

            Assert.Throws<InvalidOperationException>(() => evt.Cancel());
        }

        [Fact]
        public void Should_Not_Change_AcceptedAt()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var originalAcceptedAt = evt.AcceptedAt;
            Thread.Sleep(20);

            evt.Cancel();

            Assert.Equal(originalAcceptedAt, evt.AcceptedAt);
        }
    }

    // Auxiliary builder to create an Event with a specific status via reflection
    private class EventBuilder
    {
        private EventStatus _status = EventStatus.Pending;

        public EventBuilder WithStatus(EventStatus status)
        {
            _status = status;
            return this;
        }

        public Event Build()
        {
            var evt = new Event(ValidTenantId, ValidDestinationUrlId, ValidPayload);
            var prop = typeof(Event).GetProperty(
                nameof(Event.Status),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            prop!.GetSetMethod(nonPublic: true)!.Invoke(evt, [_status]);
            return evt;
        }
    }
}
