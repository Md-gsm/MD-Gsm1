namespace MDGsm.Persons.Application;

public sealed record PersonsOutboxDispatchResult(int LeasedCount,int DeliveredCount,int RetryScheduledCount,int LeaseLostCount);

/// <summary>One explicitly invoked delivery batch; no timer, hosted worker, broker, or default retry policy.</summary>
public sealed class PersonsOutboxDispatcher
{
    private readonly IPersonsOutboxDeliveryStore _store;
    private readonly IPersonsOutboxPublisher _publisher;
    private readonly TimeProvider _timeProvider;

    public PersonsOutboxDispatcher(IPersonsOutboxDeliveryStore store,IPersonsOutboxPublisher publisher,TimeProvider? timeProvider=null)
    {
        _store=store??throw new ArgumentNullException(nameof(store));
        _publisher=publisher??throw new ArgumentNullException(nameof(publisher));
        _timeProvider=timeProvider??TimeProvider.System;
    }

    public async Task<PersonsOutboxDispatchResult> DispatchBatchAsync(
        string leaseOwner,int batchSize,TimeSpan leaseDuration,TimeSpan retryDelay,CancellationToken cancellationToken=default)
    {
        if(leaseDuration<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        if(retryDelay<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(retryDelay));
        var now=_timeProvider.GetUtcNow();
        var leased=await _store.LeaseDueAsync(leaseOwner,batchSize,now,now.Add(leaseDuration),cancellationToken);
        var delivered=0;var retried=0;var lost=0;
        foreach(var item in leased)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try{await _publisher.PublishAsync(item.Event,cancellationToken);}
            catch(OperationCanceledException){throw;}
            catch(Exception exception) when(exception is not OutOfMemoryException)
            {
                // Transport exception text is intentionally never persisted; it can contain PII or secrets.
                var failedAt=_timeProvider.GetUtcNow();
                if(await _store.ScheduleRetryAsync(item.Event.EventId,leaseOwner,failedAt,failedAt.Add(retryDelay),
                    PersonsOutboxDeliveryFailureCodes.PublishFailed,cancellationToken))retried++;
                else lost++;
                continue;
            }
            if(await _store.MarkDeliveredAsync(item.Event.EventId,leaseOwner,_timeProvider.GetUtcNow(),cancellationToken))delivered++;
            else lost++;
        }
        return new PersonsOutboxDispatchResult(leased.Count,delivered,retried,lost);
    }
}
