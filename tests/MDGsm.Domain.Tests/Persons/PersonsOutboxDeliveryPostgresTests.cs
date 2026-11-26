using MDGsm.Persons.Application;
using MDGsm.Persons.Infrastructure;
using Npgsql;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsOutboxDeliveryPostgresTests
{
    private static readonly DateTimeOffset Start=new(2026,11,26,9,0,0,TimeSpan.Zero);
    [Fact][Trait("Category","Integration")]
    public async Task Due_event_is_leased_once_and_acknowledged()
    {
        await using var dataSource=await OpenDisposableDataSourceAsync();var eventId=await InsertPendingEventAsync(dataSource);
        var store=new PostgresPersonsOutboxDeliveryStore(dataSource);
        var first=await store.LeaseDueAsync("worker-a",10,Start,Start.AddMinutes(1),default);
        var second=await store.LeaseDueAsync("worker-b",10,Start,Start.AddMinutes(1),default);
        var leased=Assert.Single(first);Assert.Equal(eventId,leased.Event.EventId);Assert.Equal(1,leased.AttemptCount);Assert.Empty(second);
        Assert.True(await store.MarkDeliveredAsync(eventId,"worker-a",Start.AddSeconds(10),default));
        Assert.False(await store.MarkDeliveredAsync(eventId,"worker-a",Start.AddSeconds(11),default));
        Assert.Equal("delivered",await ScalarTextAsync(dataSource,"SELECT delivery_status FROM persons_outbox_deliveries WHERE event_id=@event;",eventId));
        Assert.Equal(1L,await ScalarAsync(dataSource,
            "SELECT count(*) FROM persons_outbox_deliveries WHERE event_id=@event AND delivered_at_utc=@time AND lease_owner IS NULL AND lease_expires_at_utc IS NULL;",
            eventId,Start.AddSeconds(10)));
    }
    [Fact][Trait("Category","Integration")]
    public async Task Expired_lease_is_reclaimed_after_restart_and_stale_owner_is_fenced()
    {
        await using var dataSource=await OpenDisposableDataSourceAsync();var eventId=await InsertPendingEventAsync(dataSource);
        var oldStore=new PostgresPersonsOutboxDeliveryStore(dataSource);var recoveredStore=new PostgresPersonsOutboxDeliveryStore(dataSource);
        var first=Assert.Single(await oldStore.LeaseDueAsync("process-before-restart",5,Start,Start.AddSeconds(30),default));
        Assert.Equal(1,first.AttemptCount);
        var recovered=Assert.Single(await recoveredStore.LeaseDueAsync("process-after-restart",5,Start.AddSeconds(31),Start.AddMinutes(2),default));
        Assert.Equal(eventId,recovered.Event.EventId);Assert.Equal(first.Event.EventId,recovered.Event.EventId);Assert.Equal(2,recovered.AttemptCount);
        Assert.False(await oldStore.MarkDeliveredAsync(eventId,"process-before-restart",Start.AddSeconds(32),default));
        Assert.False(await oldStore.ScheduleRetryAsync(eventId,"process-before-restart",Start.AddSeconds(32),Start.AddSeconds(40),
            PersonsOutboxDeliveryFailureCodes.PublishFailed,default));
        Assert.Equal(PersonsOutboxDeliveryFailureCodes.LeaseExpired,await ScalarTextAsync(dataSource,
            "SELECT last_error_code FROM persons_outbox_deliveries WHERE event_id=@event;",eventId));
        Assert.True(await recoveredStore.MarkDeliveredAsync(eventId,"process-after-restart",Start.AddSeconds(33),default));
    }
    [Fact][Trait("Category","Integration")]
    public async Task Retry_waits_until_its_due_time_and_reuses_the_original_event_id()
    {
        await using var dataSource=await OpenDisposableDataSourceAsync();var eventId=await InsertPendingEventAsync(dataSource);
        var store=new PostgresPersonsOutboxDeliveryStore(dataSource);
        var first=Assert.Single(await store.LeaseDueAsync("worker-a",5,Start,Start.AddMinutes(1),default));
        Assert.True(await store.ScheduleRetryAsync(eventId,"worker-a",Start.AddSeconds(5),Start.AddSeconds(45),PersonsOutboxDeliveryFailureCodes.PublishFailed,default));
        Assert.Empty(await store.LeaseDueAsync("worker-b",5,Start.AddSeconds(44),Start.AddMinutes(2),default));
        var retry=Assert.Single(await store.LeaseDueAsync("worker-after-retry",5,Start.AddSeconds(46),Start.AddMinutes(3),default));
        Assert.Equal(first.Event.EventId,retry.Event.EventId);Assert.Equal(2,retry.AttemptCount);
        Assert.Equal(1L,await ScalarAsync(dataSource,"SELECT count(*) FROM persons_outbox_events WHERE event_id=@event;",eventId));
    }
    [Fact][Trait("Category","Integration")]
    public async Task Concurrent_workers_cannot_lease_the_same_due_event()
    {
        await using var dataSource=await OpenDisposableDataSourceAsync();var eventId=await InsertPendingEventAsync(dataSource);
        var store=new PostgresPersonsOutboxDeliveryStore(dataSource);
        var results=await Task.WhenAll(store.LeaseDueAsync("worker-a",1,Start,Start.AddMinutes(1),default),
            store.LeaseDueAsync("worker-b",1,Start,Start.AddMinutes(1),default));
        var leased=Assert.Single(results.SelectMany(x=>x));Assert.Equal(eventId,leased.Event.EventId);
    }
    [Fact][Trait("Category","Integration")]
    public async Task Event_envelope_is_immutable_and_contains_no_business_payload_columns()
    {
        await using var dataSource=await OpenDisposableDataSourceAsync();var eventId=await InsertPendingEventAsync(dataSource);
        await ExecuteAsync(dataSource,"UPDATE persons_outbox_events SET actor_reference='tampered' WHERE event_id=@event;",eventId);
        await ExecuteAsync(dataSource,"DELETE FROM persons_outbox_events WHERE event_id=@event;",eventId);
        Assert.Equal("actor-reference",await ScalarTextAsync(dataSource,"SELECT actor_reference FROM persons_outbox_events WHERE event_id=@event;",eventId));
        Assert.Equal(1L,await ScalarAsync(dataSource,"SELECT count(*) FROM persons_outbox_events WHERE event_id=@event;",eventId));
        var columns=await ReadColumnsAsync(dataSource,"persons_outbox_events");
        Assert.DoesNotContain("payload",columns,StringComparer.OrdinalIgnoreCase);Assert.DoesNotContain("name",columns,StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("mobile",columns,StringComparer.OrdinalIgnoreCase);Assert.DoesNotContain("email",columns,StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("address",columns,StringComparer.OrdinalIgnoreCase);
    }
    private static async Task<NpgsqlDataSource> OpenDisposableDataSourceAsync()
    {
        var cs=Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
        Assert.False(string.IsNullOrWhiteSpace(cs),"Persons Outbox tests require the disposable PostgreSQL database supplied by CI.");
        var ds=NpgsqlDataSource.Create(cs!);await using var conn=await ds.OpenConnectionAsync();
        await using var marker=new NpgsqlCommand("SELECT coalesce(shobj_description(oid,'pg_database'),'') FROM pg_database WHERE datname=current_database();",conn);
        Assert.Contains("MDGSM_DISPOSABLE_TEST_DB",(string?)await marker.ExecuteScalarAsync()??string.Empty);return ds;
    }
    private static async Task<Guid> InsertPendingEventAsync(NpgsqlDataSource ds)
    {
        var party=Guid.CreateVersion7();var eventId=Guid.CreateVersion7();var operation=Guid.CreateVersion7();
        await using var conn=await ds.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync();
        await using(var p=new NpgsqlCommand("""
            INSERT INTO persons (party_id,party_type,business_roles,given_name,family_name,acquaintance_source,internal_note,created_by,updated_by)
            VALUES (@party,1,1,'Outbox','Test',0,'','outbox-test','outbox-test');
            """,conn,tx)){p.Parameters.AddWithValue("party",party);await p.ExecuteNonQueryAsync();}
        await using(var e=new NpgsqlCommand("""
            INSERT INTO persons_outbox_events (event_id,event_type,contract_version,aggregate_id,actor_reference,occurred_at_utc,idempotency_key)
            VALUES (@event,'PartyRegistered','1.0',@party,'actor-reference',@occurred,@operation);
            """,conn,tx))
        {e.Parameters.AddWithValue("event",eventId);e.Parameters.AddWithValue("party",party);e.Parameters.AddWithValue("occurred",Start);e.Parameters.AddWithValue("operation",operation);await e.ExecuteNonQueryAsync();}
        await using(var d=new NpgsqlCommand("""
            INSERT INTO persons_outbox_deliveries(event_id,delivery_status,attempt_count,next_attempt_at_utc)
            VALUES (@event,'pending',0,@occurred);
            """,conn,tx)){d.Parameters.AddWithValue("event",eventId);d.Parameters.AddWithValue("occurred",Start);await d.ExecuteNonQueryAsync();}
        await tx.CommitAsync();return eventId;
    }
    private static async Task<long> ScalarAsync(NpgsqlDataSource ds,string sql,Guid id,DateTimeOffset? time=null)
    {
        await using var conn=await ds.OpenConnectionAsync();await using var cmd=new NpgsqlCommand(sql,conn);
        cmd.Parameters.AddWithValue("event",id);if(time is not null)cmd.Parameters.AddWithValue("time",time.Value);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
    private static async Task<string> ScalarTextAsync(NpgsqlDataSource ds,string sql,Guid id)
    {await using var conn=await ds.OpenConnectionAsync();await using var cmd=new NpgsqlCommand(sql,conn);cmd.Parameters.AddWithValue("event",id);return (string)(await cmd.ExecuteScalarAsync())!;}
    private static async Task ExecuteAsync(NpgsqlDataSource ds,string sql,Guid id)
    {await using var conn=await ds.OpenConnectionAsync();await using var cmd=new NpgsqlCommand(sql,conn);cmd.Parameters.AddWithValue("event",id);await cmd.ExecuteNonQueryAsync();}
    private static async Task<string[]> ReadColumnsAsync(NpgsqlDataSource ds,string table)
    {
        await using var conn=await ds.OpenConnectionAsync();await using var cmd=new NpgsqlCommand("""
            SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name=@table ORDER BY ordinal_position;
            """,conn);cmd.Parameters.AddWithValue("table",table);var result=new List<string>();
        await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())result.Add(reader.GetString(0));return result.ToArray();
    }
}
