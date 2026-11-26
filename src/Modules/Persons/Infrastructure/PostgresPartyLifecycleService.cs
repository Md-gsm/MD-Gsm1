using System.Data;
using MDGsm.Persons.Application;
using Npgsql;

namespace MDGsm.Persons.Infrastructure;

/// <summary>Optimistic-concurrency lifecycle operations; history is captured by migration 0015 triggers.</summary>
public sealed class PostgresPartyLifecycleService(NpgsqlDataSource dataSource, TimeProvider timeProvider) : IPartyLifecycleService
{
    public async Task<PartyLifecycleChangeResult> SetPartyActiveAsync(Guid partyId, bool isActive,
        long expectedRowVersion, string actorId, string reason, CancellationToken cancellationToken)
    {
        ValidateIdsAndVersion(partyId, expectedRowVersion);
        var actor = ValidateActor(actorId);
        var why = ValidateReason(reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await SetContextAsync(connection, transaction, actor, why, cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE persons
            SET is_active = @active, updated_at_utc = @now, updated_by = @actor,
                last_change_reason = @reason, row_version = row_version + 1
            WHERE party_id = @id AND row_version = @version AND merged_into_party_id IS NULL
            RETURNING row_version;
            """, connection, transaction);
        command.Parameters.AddWithValue("active", isActive);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow().ToUniversalTime());
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", why);
        command.Parameters.AddWithValue("id", partyId);
        command.Parameters.AddWithValue("version", expectedRowVersion);
        var version = await command.ExecuteScalarAsync(cancellationToken);
        if (version is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PartyLifecycleChangeResult(PartyLifecycleChangeStatus.Applied, (long)version);
        }
        var current = await ReadPartyStateAsync(connection, transaction, partyId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return current is null
            ? new PartyLifecycleChangeResult(PartyLifecycleChangeStatus.NotFound, null)
            : current.Value.Merged
                ? new PartyLifecycleChangeResult(PartyLifecycleChangeStatus.PartyInactiveOrMerged, current.Value.Version)
                : new PartyLifecycleChangeResult(PartyLifecycleChangeStatus.VersionConflict, current.Value.Version);
    }

    public async Task<PartyLifecycleChangeResult> SetContactPointActiveAsync(Guid contactPointId, bool isActive,
        long expectedRowVersion, string actorId, string reason, CancellationToken cancellationToken)
    {
        ValidateIdsAndVersion(contactPointId, expectedRowVersion);
        var actor = ValidateActor(actorId); var why = ValidateReason(reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await SetContextAsync(connection, transaction, actor, why, cancellationToken);
        var row = await ReadContactAsync(connection, transaction, contactPointId, cancellationToken);
        if (row is null) { await transaction.CommitAsync(cancellationToken); return new(PartyLifecycleChangeStatus.NotFound, null); }
        if (row.Value.Version != expectedRowVersion) { await transaction.CommitAsync(cancellationToken); return new(PartyLifecycleChangeStatus.VersionConflict, row.Value.Version); }
        if (!await IsPartyActiveAsync(connection, transaction, row.Value.PartyId, cancellationToken))
        { await transaction.CommitAsync(cancellationToken); return new(PartyLifecycleChangeStatus.PartyInactiveOrMerged, row.Value.Version); }
        if (row.Value.Active == isActive) { await transaction.CommitAsync(cancellationToken); return new(PartyLifecycleChangeStatus.Applied, row.Value.Version); }

        await using (var update = new NpgsqlCommand("""
            UPDATE persons_contact_points
            SET is_active = @active,
                is_primary = CASE WHEN @active THEN NOT EXISTS (
                    SELECT 1 FROM persons_contact_points other
                    WHERE other.party_id = @party AND other.kind = @kind AND other.is_active
                      AND other.is_primary AND other.contact_point_id <> @id)
                    ELSE false END
            WHERE contact_point_id = @id AND row_version = @version;
            """, connection, transaction))
        {
            update.Parameters.AddWithValue("active", isActive);
            update.Parameters.AddWithValue("party", row.Value.PartyId);
            update.Parameters.AddWithValue("kind", row.Value.Kind);
            update.Parameters.AddWithValue("id", contactPointId);
            update.Parameters.AddWithValue("version", expectedRowVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            { await transaction.RollbackAsync(cancellationToken); return new(PartyLifecycleChangeStatus.VersionConflict, null); }
        }
        if (!isActive && row.Value.Primary)
            await PromoteContactAsync(connection, transaction, row.Value.PartyId, row.Value.Kind, actor, why, cancellationToken);
        var next = await ReadContactAsync(connection, transaction, contactPointId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PartyLifecycleChangeStatus.Applied, next?.Version);
    }

    public async Task<PartyLifecycleChangeResult> UpdateContactPointValueAsync(Guid contactPointId, string value,
        long expectedRowVersion, string actorId, string reason, CancellationToken cancellationToken)
    {
        ValidateIdsAndVersion(contactPointId, expectedRowVersion);
        var actor=ValidateActor(actorId); var why=ValidateReason(reason); var normalized=ValidateText(value,254,nameof(value));
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted,cancellationToken);
        await SetContextAsync(connection,transaction,actor,why,cancellationToken);
        var row=await ReadContactAsync(connection,transaction,contactPointId,cancellationToken);
        if(row is null){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.NotFound,null);}
        if(row.Value.Version!=expectedRowVersion){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.VersionConflict,row.Value.Version);}
        if(!await IsPartyActiveAsync(connection,transaction,row.Value.PartyId,cancellationToken)){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.PartyInactiveOrMerged,row.Value.Version);}
        await using var update=new NpgsqlCommand("UPDATE persons_contact_points SET value=@value WHERE contact_point_id=@id AND row_version=@version;",connection,transaction);
        update.Parameters.AddWithValue("value",normalized);update.Parameters.AddWithValue("id",contactPointId);update.Parameters.AddWithValue("version",expectedRowVersion);
        if(await update.ExecuteNonQueryAsync(cancellationToken)!=1){await transaction.RollbackAsync(cancellationToken);return new(PartyLifecycleChangeStatus.VersionConflict,null);}
        var next=await ReadContactAsync(connection,transaction,contactPointId,cancellationToken);
        await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.Applied,next?.Version);
    }

    public async Task<PartyLifecycleChangeResult> SetAddressActiveAsync(Guid addressId, bool isActive,
        long expectedRowVersion, string actorId, string reason, CancellationToken cancellationToken)
    {
        ValidateIdsAndVersion(addressId,expectedRowVersion);
        var actor=ValidateActor(actorId);var why=ValidateReason(reason);
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted,cancellationToken);
        await SetContextAsync(connection,transaction,actor,why,cancellationToken);
        var row=await ReadAddressAsync(connection,transaction,addressId,cancellationToken);
        if(row is null){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.NotFound,null);}
        if(row.Value.Version!=expectedRowVersion){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.VersionConflict,row.Value.Version);}
        if(!await IsPartyActiveAsync(connection,transaction,row.Value.PartyId,cancellationToken)){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.PartyInactiveOrMerged,row.Value.Version);}
        if(row.Value.Active==isActive){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.Applied,row.Value.Version);}
        await using(var update=new NpgsqlCommand("""
            UPDATE persons_addresses
            SET is_active=@active,
                is_primary=CASE WHEN @active THEN NOT EXISTS (
                    SELECT 1 FROM persons_addresses other
                    WHERE other.party_id=@party AND other.is_active AND other.is_primary AND other.address_id<>@id)
                    ELSE false END
            WHERE address_id=@id AND row_version=@version;
            """,connection,transaction))
        {
            update.Parameters.AddWithValue("active",isActive);update.Parameters.AddWithValue("party",row.Value.PartyId);
            update.Parameters.AddWithValue("id",addressId);update.Parameters.AddWithValue("version",expectedRowVersion);
            if(await update.ExecuteNonQueryAsync(cancellationToken)!=1){await transaction.RollbackAsync(cancellationToken);return new(PartyLifecycleChangeStatus.VersionConflict,null);}
        }
        if(!isActive&&row.Value.Primary)
            await PromoteAddressAsync(connection,transaction,row.Value.PartyId,actor,why,cancellationToken);
        var next=await ReadAddressAsync(connection,transaction,addressId,cancellationToken);
        await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.Applied,next?.Version);
    }

    public async Task<PartyLifecycleChangeResult> UpdateAddressAsync(Guid addressId,string countryCode,string province,
        string city,string postalCode,string addressLine,long expectedRowVersion,string actorId,string reason,CancellationToken cancellationToken)
    {
        ValidateIdsAndVersion(addressId,expectedRowVersion);
        var actor=ValidateActor(actorId);var why=ValidateReason(reason);
        var country=ValidateText(countryCode,2,nameof(countryCode)).ToUpperInvariant();
        if(country.Length!=2)throw new ArgumentException("Country code must be two letters.",nameof(countryCode));
        var prov=ValidateText(province,100,nameof(province));var town=ValidateText(city,100,nameof(city));
        var postal=ValidateText(postalCode,20,nameof(postalCode));var line=ValidateText(addressLine,1000,nameof(addressLine));
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted,cancellationToken);
        await SetContextAsync(connection,transaction,actor,why,cancellationToken);
        var row=await ReadAddressAsync(connection,transaction,addressId,cancellationToken);
        if(row is null){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.NotFound,null);}
        if(row.Value.Version!=expectedRowVersion){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.VersionConflict,row.Value.Version);}
        if(!await IsPartyActiveAsync(connection,transaction,row.Value.PartyId,cancellationToken)){await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.PartyInactiveOrMerged,row.Value.Version);}
        await using var update=new NpgsqlCommand("""
            UPDATE persons_addresses SET country_code=@country,province=@province,city=@city,
                postal_code=@postal,address_line=@line
            WHERE address_id=@id AND row_version=@version;
            """,connection,transaction);
        update.Parameters.AddWithValue("country",country);update.Parameters.AddWithValue("province",prov);
        update.Parameters.AddWithValue("city",town);update.Parameters.AddWithValue("postal",postal);
        update.Parameters.AddWithValue("line",line);update.Parameters.AddWithValue("id",addressId);
        update.Parameters.AddWithValue("version",expectedRowVersion);
        if(await update.ExecuteNonQueryAsync(cancellationToken)!=1){await transaction.RollbackAsync(cancellationToken);return new(PartyLifecycleChangeStatus.VersionConflict,null);}
        var next=await ReadAddressAsync(connection,transaction,addressId,cancellationToken);
        await transaction.CommitAsync(cancellationToken);return new(PartyLifecycleChangeStatus.Applied,next?.Version);
    }

    private static async Task SetContextAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string actor,string reason,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("""
            SELECT set_config('persons.lifecycle_actor',@actor,true),
                   set_config('persons.lifecycle_reason',@reason,true),
                   set_config('persons.lifecycle_operation',@operation,true),
                   set_config('persons.lifecycle_occurred_at_utc',@occurred,true);
            """,connection,transaction);
        command.Parameters.AddWithValue("actor",actor);command.Parameters.AddWithValue("reason",reason);
        command.Parameters.AddWithValue("operation",Guid.CreateVersion7().ToString("D"));
        command.Parameters.AddWithValue("occurred",DateTimeOffset.UtcNow.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> IsPartyActiveAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid partyId,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("SELECT is_active AND merged_into_party_id IS NULL FROM persons WHERE party_id=@id;",connection,transaction);
        command.Parameters.AddWithValue("id",partyId);return (bool?)await command.ExecuteScalarAsync(cancellationToken)==true;
    }

    private static async Task<(long Version,bool Merged)?> ReadPartyStateAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid id,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("SELECT row_version, merged_into_party_id IS NOT NULL FROM persons WHERE party_id=@id;",connection,transaction);
        command.Parameters.AddWithValue("id",id);await using var reader=await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)?(reader.GetInt64(0),reader.GetBoolean(1)):null;
    }

    private static async Task<(Guid PartyId,short Kind,long Version,bool Active,bool Primary)?> ReadContactAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid id,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("SELECT party_id,kind,row_version,is_active,is_primary FROM persons_contact_points WHERE contact_point_id=@id FOR UPDATE;",connection,transaction);
        command.Parameters.AddWithValue("id",id);await using var reader=await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)?(reader.GetGuid(0),reader.GetInt16(1),reader.GetInt64(2),reader.GetBoolean(3),reader.GetBoolean(4)):null;
    }

    private static async Task<(Guid PartyId,long Version,bool Active,bool Primary)?> ReadAddressAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid id,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("SELECT party_id,row_version,is_active,is_primary FROM persons_addresses WHERE address_id=@id FOR UPDATE;",connection,transaction);
        command.Parameters.AddWithValue("id",id);await using var reader=await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)?(reader.GetGuid(0),reader.GetInt64(1),reader.GetBoolean(2),reader.GetBoolean(3)):null;
    }

    private static async Task PromoteContactAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid party,short kind,string actor,string reason,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("""
            UPDATE persons_contact_points SET is_primary=true
            WHERE contact_point_id=(SELECT contact_point_id FROM persons_contact_points
                WHERE party_id=@party AND kind=@kind AND is_active AND NOT is_primary
                ORDER BY created_at_utc,contact_point_id LIMIT 1)
              AND NOT EXISTS (SELECT 1 FROM persons_contact_points WHERE party_id=@party AND kind=@kind AND is_active AND is_primary);
            """,connection,transaction);
        command.Parameters.AddWithValue("party",party);command.Parameters.AddWithValue("kind",kind);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task PromoteAddressAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid party,string actor,string reason,CancellationToken cancellationToken)
    {
        await using var command=new NpgsqlCommand("""
            UPDATE persons_addresses SET is_primary=true
            WHERE address_id=(SELECT address_id FROM persons_addresses
                WHERE party_id=@party AND is_active AND NOT is_primary
                ORDER BY created_at_utc,address_id LIMIT 1)
              AND NOT EXISTS (SELECT 1 FROM persons_addresses WHERE party_id=@party AND is_active AND is_primary);
            """,connection,transaction);
        command.Parameters.AddWithValue("party",party);await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateIdsAndVersion(Guid id,long version)
    { if(id==Guid.Empty)throw new ArgumentException("A non-empty entity id is required.");if(version<1)throw new ArgumentOutOfRangeException(nameof(version)); }
    private static string ValidateActor(string value)
    { if(string.IsNullOrWhiteSpace(value)||value.Length>128||value.Any(char.IsControl))throw new ArgumentException("A trusted actor identifier is required.",nameof(value));return value.Trim(); }
    private static string ValidateReason(string value)
    { if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>300||value.Any(char.IsControl))throw new ArgumentException("A lifecycle reason of 1-300 characters is required.",nameof(value));return value.Trim(); }
    private static string ValidateText(string value,int max,string name)
    { if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("A non-empty value is required.",name);var v=value.Trim();if(v.Length>max)throw new ArgumentException($"Value cannot exceed {max} characters.",name);return v; }
}
