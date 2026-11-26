using MDGsm.Persons.Application;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsOutboxDispatcherTests
{
    private static readonly DateTimeOffset Now=new(2026,11,26,9,0,0,TimeSpan.Zero);
    [Fact]
    public async Task Successful_publish_is_acknowledged_with_same_event_id()
    {
        var item=NewLease();var store=new FakeStore([item]);var publisher=new FakePublisher();
        var dispatcher=new PersonsOutboxDispatcher(store,publisher,new FixedTimeProvider(Now));
        var result=await dispatcher.DispatchBatchAsync("dispatcher-a",10,TimeSpan.FromMinutes(1),TimeSpan.FromSeconds(30));
        Assert.Equal(new PersonsOutboxDispatchResult(1,1,0,0),result);
        Assert.Equal(item.Event.EventId,publisher.Published.Single().EventId);
        Assert.Equal(item.Event.EventId,store.Delivered.Single());Assert.Empty(store.Retries);
    }
    [Fact]
    public async Task Publish_failure_schedules_safe_retry_without_persisting_exception_text()
    {
        var item=NewLease();var store=new FakeStore([item]);var publisher=new FakePublisher(new InvalidOperationException("sensitive transport detail"));
        var dispatcher=new PersonsOutboxDispatcher(store,publisher,new FixedTimeProvider(Now));
        var result=await dispatcher.DispatchBatchAsync("dispatcher-a",10,TimeSpan.FromMinutes(1),TimeSpan.FromSeconds(45));
        Assert.Equal(new PersonsOutboxDispatchResult(1,0,1,0),result);
        var retry=Assert.Single(store.Retries);Assert.Equal(item.Event.EventId,retry.EventId);
        Assert.Equal(PersonsOutboxDeliveryFailureCodes.PublishFailed,retry.FailureCode);
        Assert.Equal(Now.AddSeconds(45),retry.NextAttemptAtUtc);Assert.Empty(store.Delivered);
    }
    [Fact]
    public async Task Stale_lease_acknowledgement_is_reported_as_lease_lost()
    {
        var store=new FakeStore([NewLease()]){AcknowledgeResult=false};var publisher=new FakePublisher();
        var dispatcher=new PersonsOutboxDispatcher(store,publisher,new FixedTimeProvider(Now));
        var result=await dispatcher.DispatchBatchAsync("dispatcher-a",10,TimeSpan.FromMinutes(1),TimeSpan.FromSeconds(30));
        Assert.Equal(new PersonsOutboxDispatchResult(1,0,0,1),result);Assert.Single(publisher.Published);Assert.Empty(store.Delivered);
    }
    [Fact]
    public async Task Cancellation_does_not_acknowledge_or_schedule_an_uncertain_publish()
    {
        var store=new FakeStore([NewLease()]);var publisher=new FakePublisher(new OperationCanceledException());
        var dispatcher=new PersonsOutboxDispatcher(store,publisher,new FixedTimeProvider(Now));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>
            dispatcher.DispatchBatchAsync("dispatcher-a",10,TimeSpan.FromMinutes(1),TimeSpan.FromSeconds(30)));
        Assert.Empty(store.Delivered);Assert.Empty(store.Retries);
    }
    private static LeasedPersonsOutboxEvent NewLease()
    {
        var message=new PersonsOutboxEvent(Guid.CreateVersion7(),PersonsOutboxEventContract.PartyRegistered,
            PersonsOutboxEventContract.PartyRegisteredVersion,Guid.CreateVersion7(),"actor-reference",Now,null,null,Guid.CreateVersion7());
        return new LeasedPersonsOutboxEvent(message,1);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
    private sealed class FakePublisher(Exception? failure=null):IPersonsOutboxPublisher
    {
        public List<PersonsOutboxEvent> Published{get;}=[];
        public Task PublishAsync(PersonsOutboxEvent message,CancellationToken cancellationToken)
        {if(failure is not null)throw failure;Published.Add(message);return Task.CompletedTask;}
    }
    private sealed class FakeStore(IReadOnlyList<LeasedPersonsOutboxEvent> leases):IPersonsOutboxDeliveryStore
    {
        public bool AcknowledgeResult{get;init;}=true;
        public List<Guid> Delivered{get;}=[];
        public List<(Guid EventId,DateTimeOffset NextAttemptAtUtc,string FailureCode)> Retries{get;}=[];
        public Task<IReadOnlyList<LeasedPersonsOutboxEvent>> LeaseDueAsync(string leaseOwner,int batchSize,DateTimeOffset nowUtc,
            DateTimeOffset leaseExpiresAtUtc,CancellationToken cancellationToken)=>Task.FromResult(leases);
        public Task<bool> MarkDeliveredAsync(Guid eventId,string leaseOwner,DateTimeOffset deliveredAtUtc,CancellationToken cancellationToken)
        {if(AcknowledgeResult)Delivered.Add(eventId);return Task.FromResult(AcknowledgeResult);}
        public Task<bool> ScheduleRetryAsync(Guid eventId,string leaseOwner,DateTimeOffset nowUtc,DateTimeOffset nextAttemptAtUtc,
            string failureCode,CancellationToken cancellationToken)
        {Retries.Add((eventId,nextAttemptAtUtc,failureCode));return Task.FromResult(true);}
    }
}
