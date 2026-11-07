using System.Data.Common;
using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using Npgsql;
using NpgsqlTypes;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// PostgreSQL implementation for Persons registration and read operations.
/// Writes are parameterized, atomic and idempotent by OperationId; reads are audited and mask sensitive values by default.
/// Registration/detail reads join a caller transaction when supplied. Access-audit records commit independently so caller rollback cannot erase them.
/// Sensitive reads require server-side authorization; includeSensitive alone never grants access. After a
/// <see cref="DuplicatePartyIdentifierException"/> an ambient transaction is aborted by PostgreSQL and must be rolled back.
/// Actor identity must come from the trusted API boundary.
/// </summary>
public sealed class PostgresPartyRepository(NpgsqlDataSource dataSource, IPartySensitiveDataAuthorizer? sensitiveDataAuthorizer = null) : IPartyRepository
{
    private readonly IPartySensitiveDataAuthorizer _sensitiveDataAuthorizer = sensitiveDataAuthorizer ?? new DenyByDefaultPartySensitiveDataAuthorizer();
    public Task<PartyRegistrationResult> AddNaturalAsync(
        RegisterNaturalParty request, string actorId, Guid operationId, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PartyRegistrationRules.ValidateRoles(request.Roles);
        PartyRegistrationRules.ValidateWorkClassification(PartyType.NaturalPerson, request.Roles, request.WorkClassification);
        var party = NaturalPerson.Create(request.GivenName, request.FamilyName, request.Roles);
        var nationalId = PartyIdentifiers.NormalizeIranianNationalId(request.NationalId);
        return InsertAsync(party.Id, PartyType.NaturalPerson, request.Roles,
            party.GivenName, party.FamilyName, null, nationalId, null, null, null,
            request, actorId, operationId, transaction, cancellationToken);
    }

    public Task<PartyRegistrationResult> AddLegalAsync(
        RegisterLegalParty request, string actorId, Guid operationId, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PartyRegistrationRules.ValidateRoles(request.Roles);
        PartyRegistrationRules.ValidateWorkClassification(PartyType.LegalPerson, request.Roles, request.WorkClassification);
        var party = LegalPerson.Create(request.LegalName, request.Roles);
        var legalNationalId = PartyIdentifiers.NormalizeIranianLegalNationalId(request.LegalNationalId);
        var economicCode = PartyIdentifiers.NormalizeEconomicCode(request.EconomicCode);
        var responsible = OptionalText(request.ResponsiblePersonName, 200, nameof(request.ResponsiblePersonName));
        return InsertAsync(party.Id, PartyType.LegalPerson, request.Roles,
            null, null, party.LegalName, null, legalNationalId, economicCode, responsible,
            request, actorId, operationId, transaction, cancellationToken);
    }

    public Task<PartyQuickRegistrationResult> QuickRegisterNaturalAsync(
        RegisterNaturalParty request, string actorId, Guid operationId, bool forceNew,
        string? selectedPartyId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PartyRegistrationRules.ValidateRoles(request.Roles);
        PartyRegistrationRules.ValidateWorkClassification(PartyType.NaturalPerson, request.Roles, request.WorkClassification);
        return QuickRegisterAsync(request, PartyType.NaturalPerson,
            PersianText.Normalize($"{request.GivenName} {request.FamilyName}"),
            actorId, operationId, forceNew, selectedPartyId, transaction,
            () => AddNaturalAsync(request, actorId, operationId, transaction, cancellationToken), cancellationToken);
    }

    public Task<PartyQuickRegistrationResult> QuickRegisterLegalAsync(
        RegisterLegalParty request, string actorId, Guid operationId, bool forceNew,
        string? selectedPartyId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PartyRegistrationRules.ValidateRoles(request.Roles);
        PartyRegistrationRules.ValidateWorkClassification(PartyType.LegalPerson, request.Roles, request.WorkClassification);
        return QuickRegisterAsync(request, PartyType.LegalPerson, PersianText.Normalize(request.LegalName),
            actorId, operationId, forceNew, selectedPartyId, transaction,
            () => AddLegalAsync(request, actorId, operationId, transaction, cancellationToken), cancellationToken);
    }

    private async Task<PartyQuickRegistrationResult> QuickRegisterAsync(
        IPartyRegistrationCommon request, PartyType requestedType, string requestedName,
        string actorId, Guid operationId, bool forceNew, string? selectedPartyId,
        DbTransaction ambientTransaction, Func<Task<PartyRegistrationResult>> create,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ambientTransaction);
        if (ambientTransaction is not NpgsqlTransaction transaction)
            throw new ArgumentException("Quick registration requires an active PostgreSQL transaction.", nameof(ambientTransaction));
        var connection = transaction.Connection;
        if (connection is null || connection.State != System.Data.ConnectionState.Open)
            throw new ArgumentException("Quick registration requires an open PostgreSQL connection.", nameof(ambientTransaction));
        if (operationId == Guid.Empty)
            throw new ArgumentException("A non-empty OperationId is required for quick registration.", nameof(operationId));
        if (forceNew && !string.IsNullOrWhiteSpace(selectedPartyId))
            throw new ArgumentException("forceNew and selectedPartyId cannot be used together.", nameof(selectedPartyId));
        var actor = RequireActor(actorId);

        // Lock order is always OperationId first, then normalized mobile. This makes retries stable
        // and prevents two quick-registration stations from creating the same mobile concurrently.
        await using (var operationLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));", connection, transaction))
        {
            operationLock.Parameters.AddWithValue("key", operationId.ToString("D"));
            await operationLock.ExecuteScalarAsync(cancellationToken);
        }

        await using (var replayCommand = new NpgsqlCommand(
            "SELECT party_id FROM persons_operations WHERE operation_id = @operation;", connection, transaction))
        {
            replayCommand.Parameters.AddWithValue("operation", operationId);
            var replay = await replayCommand.ExecuteScalarAsync(cancellationToken);
            if (replay is Guid replayPartyId)
                return new PartyQuickRegistrationResult(PartyQuickRegistrationOutcome.Replayed,
                    replayPartyId.ToString("D"), false, Array.Empty<PartyQuickRegistrationCandidate>());
        }

        var normalizedMobile = string.IsNullOrWhiteSpace(request.PrimaryMobile)
            ? null
            : PartyContactPoint.Mobile(request.PrimaryMobile).Value;

        if (normalizedMobile is null && !string.IsNullOrWhiteSpace(selectedPartyId))
            throw new ArgumentException("Selecting an existing Party requires a mobile number to validate the candidate.", nameof(selectedPartyId));

        if (normalizedMobile is not null)
        {
            // forceNew still takes the same lock so it cannot race with an ordinary quick registration.
            await using (var mobileLock = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));", connection, transaction))
            {
                mobileLock.Parameters.AddWithValue("key", "persons-mobile:" + normalizedMobile);
                await mobileLock.ExecuteScalarAsync(cancellationToken);
            }

            var candidates = forceNew
                ? Array.Empty<PartyQuickRegistrationCandidate>()
                : await FindMobileCandidatesAsync(connection, transaction, normalizedMobile, cancellationToken);
            if (!string.IsNullOrWhiteSpace(selectedPartyId))
            {
                var selected = candidates.SingleOrDefault(x =>
                    string.Equals(x.PartyId, selectedPartyId, StringComparison.OrdinalIgnoreCase));
                if (selected is null || !selected.PartyIsActive || selected.IsMerged || !selected.HasActiveMobile || selected.Type != requestedType)
                    throw new InvalidOperationException("The selected Party is not an active, unmerged candidate of the requested identity type.");
                await AddBusinessRolesAsync(connection, transaction, Guid.Parse(selected.PartyId), request, actor, cancellationToken);
                await RecordQuickOperationAsync(connection, transaction, operationId, Guid.Parse(selected.PartyId), actor, cancellationToken);
                return new PartyQuickRegistrationResult(PartyQuickRegistrationOutcome.ExistingPartyMatched,
                    selected.PartyId, false, candidates);
            }

            if (candidates.Count > 0)
            {
                if (candidates.Count == 1)
                {
                    var candidate = candidates[0];
                    if (candidate.PartyIsActive && !candidate.IsMerged && candidate.HasActiveMobile &&
                        candidate.Type == requestedType &&
                        string.Equals(PersianText.Normalize(candidate.DisplayName), requestedName, StringComparison.Ordinal))
                    {
                        var existingId = Guid.Parse(candidate.PartyId);
                        await AddBusinessRolesAsync(connection, transaction, existingId, request, actor, cancellationToken);
                        await RecordQuickOperationAsync(connection, transaction, operationId, existingId, actor, cancellationToken);
                        return new PartyQuickRegistrationResult(PartyQuickRegistrationOutcome.ExistingPartyMatched,
                            candidate.PartyId, false, candidates);
                    }
                }

                return new PartyQuickRegistrationResult(PartyQuickRegistrationOutcome.SelectionRequired,
                    null, false, candidates);
            }
        }

        var registration = await create();
        return new PartyQuickRegistrationResult(
            registration.Created ? PartyQuickRegistrationOutcome.Created : PartyQuickRegistrationOutcome.Replayed,
            registration.PartyId, registration.Created, Array.Empty<PartyQuickRegistrationCandidate>());
    }

    private static async Task<IReadOnlyList<PartyQuickRegistrationCandidate>> FindMobileCandidatesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string normalizedMobile,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT p.party_id, p.party_type, p.business_roles,
                   CASE WHEN p.party_type = 1 THEN concat_ws(' ', p.given_name, p.family_name) ELSE p.legal_name END,
                   p.is_active, (p.merged_into_party_id IS NOT NULL), bool_or(c.is_active)
            FROM persons p
            JOIN persons_contact_points c ON c.party_id = p.party_id
            WHERE c.kind = 1 AND c.value = @mobile
            GROUP BY p.party_id, p.party_type, p.business_roles, p.given_name, p.family_name,
                     p.legal_name, p.is_active, p.merged_into_party_id
            ORDER BY p.is_active DESC, bool_or(c.is_active) DESC, p.party_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("mobile", normalizedMobile);
        var results = new List<PartyQuickRegistrationCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(new PartyQuickRegistrationCandidate(
                reader.GetGuid(0).ToString("D"), (PartyType)reader.GetInt16(1),
                reader.GetString(3), (PartyBusinessRoles)reader.GetInt16(2),
                reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6),
                PartyIdentifiers.MaskPhone(normalizedMobile)));
        return results;
    }

    private static async Task AddBusinessRolesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid partyId,
        IPartyRegistrationCommon request, string actor, CancellationToken cancellationToken)
    {
        await using (var lockCommand = new NpgsqlCommand("""
            SELECT party_id
            FROM persons
            WHERE party_id = @party AND is_active = true AND merged_into_party_id IS NULL
            FOR UPDATE;
            """, connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("party", partyId);
            if (await lockCommand.ExecuteScalarAsync(cancellationToken) is null)
                throw new InvalidOperationException("The selected Party is no longer active and unmerged.");
        }

        await using var command = new NpgsqlCommand("""
            UPDATE persons
            SET business_roles = (business_roles | @roles),
                updated_at_utc = now(),
                updated_by = @actor,
                row_version = row_version + 1
            WHERE party_id = @party
              AND is_active = true
              AND merged_into_party_id IS NULL
              AND (business_roles | @roles) <> business_roles;
            """, connection, transaction);
        var roles = request switch
        {
            RegisterNaturalParty natural => natural.Roles,
            RegisterLegalParty legal => legal.Roles,
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        command.Parameters.AddWithValue("roles", (short)roles);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("party", partyId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RecordQuickOperationAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId,
        Guid partyId, string actor, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO persons_operations (operation_id, party_id, actor)
            VALUES (@operation, @party, @actor);
            """, connection, transaction);
        command.Parameters.AddWithValue("operation", operationId);
        command.Parameters.AddWithValue("party", partyId);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private Task<PartyRegistrationResult> InsertAsync(
        PartyId partyId, PartyType partyType, PartyBusinessRoles roles,
        string? givenName, string? familyName, string? legalName,
        string? nationalId, string? legalNationalId, string? economicCode, string? responsible,
        IPartyRegistrationCommon request, string actorId, Guid operationId,
        DbTransaction? ambientTransaction, CancellationToken cancellationToken)
    {
        var actor = RequireActor(actorId);
        if (operationId == Guid.Empty)
            throw new ArgumentException("A non-empty OperationId is required for idempotent registration.", nameof(operationId));
        if (!Enum.IsDefined(request.AcquaintanceSource))
            throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentNullException.ThrowIfNull(request.CommunicationPreferences);
        var note = PartyRegistrationRules.RequireInternalNote(request.InternalNote);
        var contacts = new List<PartyContactPoint>();
        if (!string.IsNullOrWhiteSpace(request.PrimaryMobile))
        {
            contacts.Add(PartyContactPoint.Mobile(request.PrimaryMobile, true,
                request.CommunicationPreferences.RepairStatusSmsEnabled || request.CommunicationPreferences.ReadyForDeliverySmsEnabled));
        }
        if (!string.IsNullOrWhiteSpace(request.AlternateMobile)) contacts.Add(PartyContactPoint.Mobile(request.AlternateMobile));
        if (!string.IsNullOrWhiteSpace(request.Landline)) contacts.Add(PartyContactPoint.Landline(request.Landline));
        if (!string.IsNullOrWhiteSpace(request.Email)) contacts.Add(PartyContactPoint.Email(request.Email));
        if (contacts.Where(x => x.Kind == ContactPointKind.Mobile).Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() !=
            contacts.Count(x => x.Kind == ContactPointKind.Mobile))
            throw new ArgumentException("Primary and alternate mobile numbers must be different.", nameof(request));

        PartyAddress? address = null;
        var addressFields = new[] { request.Province, request.City, request.PostalCode, request.AddressLine };
        if (addressFields.Any(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (addressFields.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Province, city, postal code and full address must be supplied together.", nameof(request));
            address = PartyAddress.Create(request.Province!, request.City!, request.PostalCode!, request.AddressLine!);
        }

        var workClassification = request.WorkClassification is { } wc ? (short?)(short)wc : null;

        return RunAsync<PartyRegistrationResult>(ambientTransaction, async (connection, transaction) =>
        {
            // Serialize concurrent replays of the same OperationId for the duration of the transaction.
            await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@op, 0));", connection, transaction))
            {
                lockCommand.Parameters.Add(new NpgsqlParameter("op", NpgsqlDbType.Text) { Value = operationId.ToString("D") });
                await lockCommand.ExecuteScalarAsync(cancellationToken);
            }

            await using (var existingCommand = new NpgsqlCommand(
                "SELECT party_id FROM persons_operations WHERE operation_id = @op;", connection, transaction))
            {
                existingCommand.Parameters.AddWithValue("op", operationId);
                var existing = await existingCommand.ExecuteScalarAsync(cancellationToken);
                if (existing is Guid existingPartyId)
                    return new PartyRegistrationResult(existingPartyId.ToString("D"), false);
            }

            await using (var command = new NpgsqlCommand("""
                INSERT INTO persons
                  (party_id, party_type, business_roles, given_name, family_name, legal_name,
                   national_id, legal_national_id, economic_code, responsible_person_name,
                   acquaintance_source, internal_note, repair_status_sms_enabled,
                   ready_for_delivery_sms_enabled, promotions_sms_enabled, created_by, updated_by, work_classification)
                VALUES
                  (@id, @type, @roles, @given, @family, @legal, @national, @legalNational, @economic, @responsible,
                   @source, @note, @repairSms, @readySms, @promoSms, @actor, @actor, @workClass);
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("id", partyId.Value);
                command.Parameters.AddWithValue("type", (short)partyType);
                command.Parameters.AddWithValue("roles", (short)roles);
                AddNullable(command, "given", givenName); AddNullable(command, "family", familyName);
                AddNullable(command, "legal", legalName); AddNullable(command, "national", nationalId);
                AddNullable(command, "legalNational", legalNationalId); AddNullable(command, "economic", economicCode);
                AddNullable(command, "responsible", responsible);
                command.Parameters.AddWithValue("source", (short)request.AcquaintanceSource);
                command.Parameters.AddWithValue("note", note);
                command.Parameters.AddWithValue("repairSms", request.CommunicationPreferences.RepairStatusSmsEnabled);
                command.Parameters.AddWithValue("readySms", request.CommunicationPreferences.ReadyForDeliverySmsEnabled);
                command.Parameters.AddWithValue("promoSms", request.CommunicationPreferences.PromotionsSmsEnabled);
                command.Parameters.AddWithValue("actor", actor);
                command.Parameters.Add(new NpgsqlParameter("workClass", NpgsqlDbType.Smallint)
                {
                    Value = (object?)workClassification ?? DBNull.Value
                });
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var contact in contacts)
            {
                await using var command = new NpgsqlCommand("""
                    INSERT INTO persons_contact_points
                      (contact_point_id, party_id, kind, value, is_primary, is_messaging_allowed, created_by)
                    VALUES (@id, @party, @kind, @value, @primary, @messaging, @actor);
                    """, connection, transaction);
                command.Parameters.AddWithValue("id", Guid.CreateVersion7());
                command.Parameters.AddWithValue("party", partyId.Value);
                command.Parameters.AddWithValue("kind", (short)contact.Kind);
                command.Parameters.AddWithValue("value", contact.Value);
                command.Parameters.AddWithValue("primary", contact.IsPrimary);
                command.Parameters.AddWithValue("messaging", contact.IsMessagingAllowed);
                command.Parameters.AddWithValue("actor", actor);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (address is not null)
            {
                await using var command = new NpgsqlCommand("""
                    INSERT INTO persons_addresses
                      (address_id, party_id, country_code, province, city, postal_code, address_line, is_primary, created_by)
                    VALUES (@id, @party, @country, @province, @city, @postal, @line, true, @actor);
                    """, connection, transaction);
                command.Parameters.AddWithValue("id", Guid.CreateVersion7());
                command.Parameters.AddWithValue("party", partyId.Value);
                command.Parameters.AddWithValue("country", address.CountryCode);
                command.Parameters.AddWithValue("province", address.Province);
                command.Parameters.AddWithValue("city", address.City);
                command.Parameters.AddWithValue("postal", address.PostalCode);
                command.Parameters.AddWithValue("line", address.AddressLine);
                command.Parameters.AddWithValue("actor", actor);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var operationCommand = new NpgsqlCommand(
                "INSERT INTO persons_operations (operation_id, party_id, actor) VALUES (@op, @party, @actor);", connection, transaction))
            {
                operationCommand.Parameters.AddWithValue("op", operationId);
                operationCommand.Parameters.AddWithValue("party", partyId.Value);
                operationCommand.Parameters.AddWithValue("actor", actor);
                await operationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return new PartyRegistrationResult(partyId.ToString(), true);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PartyListItem>> SearchAsync(
        string term, int limit, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(term);
        if (limit is < 1 or > PartySearchLimits.MaxResults)
            throw new ArgumentOutOfRangeException(nameof(limit), $"At most {PartySearchLimits.MaxResults} results may be requested.");

        string? suffix = null;
        string? pattern = null;
        var digits = PartyIdentifiers.NormalizeDigits(term).Trim();
        if (digits.Length == 4 && digits.All(c => c is >= '0' and <= '9'))
        {
            suffix = digits;
        }
        else
        {
            var normalized = PersianText.Normalize(term);
            if (normalized.Length < PartySearchLimits.MinNameTermLength || normalized.Length > PartySearchLimits.MaxTermLength)
                throw new ArgumentException(
                    $"Search requires a name of {PartySearchLimits.MinNameTermLength}-{PartySearchLimits.MaxTermLength} characters or exactly four mobile digits.",
                    nameof(term));
            pattern = "%" + EscapeLike(normalized) + "%";
        }

        return RunAsync<IReadOnlyList<PartyListItem>>(transaction, async (connection, tx) =>
        {
        await using var command = new NpgsqlCommand("""
            SELECT p.party_id, p.party_type, p.business_roles,
                   CASE WHEN p.party_type = 1 THEN concat(p.given_name, ' ', p.family_name) ELSE p.legal_name END,
                   (SELECT c.value FROM persons_contact_points c WHERE c.party_id=p.party_id AND c.kind=1 AND c.is_primary AND c.is_active LIMIT 1),
                   (SELECT a.city FROM persons_addresses a WHERE a.party_id=p.party_id AND a.is_primary AND a.is_active LIMIT 1),
                   p.created_at_utc
            FROM persons p
            WHERE p.is_active AND (
              (@suffix IS NOT NULL AND EXISTS (
                 SELECT 1 FROM persons_contact_points c
                 WHERE c.party_id=p.party_id AND c.kind=1 AND c.is_active AND right(c.value, 4) = @suffix))
              OR
              (@pattern IS NOT NULL AND (
                 (p.party_type=1 AND concat_ws(' ', p.given_name, p.family_name) ILIKE @pattern ESCAPE '\')
                 OR (p.party_type=2 AND p.legal_name ILIKE @pattern ESCAPE '\')))
            )
            ORDER BY p.created_at_utc DESC, p.party_id LIMIT @limit;
            """, connection, tx);
        command.Parameters.Add(new NpgsqlParameter("suffix", NpgsqlDbType.Text) { Value = (object?)suffix ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("pattern", NpgsqlDbType.Text) { Value = (object?)pattern ?? DBNull.Value });
        command.Parameters.AddWithValue("limit", limit);
        var results = new List<PartyListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(new PartyListItem(reader.GetGuid(0).ToString("D"), (PartyType)reader.GetInt16(1),
                reader.GetString(3), (PartyBusinessRoles)reader.GetInt16(2),
                reader.IsDBNull(4) ? null : PartyIdentifiers.MaskPhone(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        return results;
        }, cancellationToken);
    }

    public async Task<PartyDetails?> GetAsync(
        string partyId, string actorId, bool includeSensitive, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        var actor = RequireActor(actorId);
        if (includeSensitive && !await _sensitiveDataAuthorizer.CanReadSensitivePartyDataAsync(actor, cancellationToken))
            throw new UnauthorizedAccessException("The actor is not authorized to read sensitive Persons data.");
        if (!PartyId.TryParse(partyId, out var id)) return null;

        var details = await RunAsync<PartyDetails?>(transaction, async (connection, tx) =>
        {
            PartyType type;
            PartyBusinessRoles roles;
            string display;
            string? national;
            string? economic;
            string? responsible;
            PartyAcquaintanceSource source;
            string note;
            PartyCommunicationPreferences preferences;
            DateTimeOffset created;
            WorkClassification? workClassification;

            await using (var command = new NpgsqlCommand("""
                SELECT party_type, business_roles, given_name, family_name, legal_name, national_id,
                       legal_national_id, economic_code, responsible_person_name, acquaintance_source,
                       internal_note, repair_status_sms_enabled, ready_for_delivery_sms_enabled,
                       promotions_sms_enabled, created_at_utc, work_classification
                FROM persons WHERE party_id=@id AND is_active;
                """, connection, tx))
            {
                command.Parameters.AddWithValue("id", id.Value);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) return null;
                type = (PartyType)reader.GetInt16(0);
                roles = (PartyBusinessRoles)reader.GetInt16(1);
                display = type == PartyType.NaturalPerson ? $"{reader.GetString(2)} {reader.GetString(3)}" : reader.GetString(4);
                national = type == PartyType.NaturalPerson
                    ? (reader.IsDBNull(5) ? null : reader.GetString(5))
                    : (reader.IsDBNull(6) ? null : reader.GetString(6));
                economic = reader.IsDBNull(7) ? null : reader.GetString(7);
                responsible = reader.IsDBNull(8) ? null : reader.GetString(8);
                source = (PartyAcquaintanceSource)reader.GetInt16(9);
                note = reader.GetString(10);
                preferences = new PartyCommunicationPreferences(reader.GetBoolean(11), reader.GetBoolean(12), reader.GetBoolean(13));
                created = reader.GetFieldValue<DateTimeOffset>(14);
                workClassification = reader.IsDBNull(15) ? null : (WorkClassification)reader.GetInt16(15);
            }

            var contacts = new List<ContactPointDto>();
            await using (var contactCommand = new NpgsqlCommand("""
                SELECT kind, value, is_primary, is_messaging_allowed FROM persons_contact_points
                WHERE party_id=@id AND is_active ORDER BY is_primary DESC, kind, contact_point_id;
                """, connection, tx))
            {
                contactCommand.Parameters.AddWithValue("id", id.Value);
                await using var cr = await contactCommand.ExecuteReaderAsync(cancellationToken);
                while (await cr.ReadAsync(cancellationToken))
                {
                    var kind = (ContactPointKind)cr.GetInt16(0);
                    var value = cr.GetString(1);
                    if (!includeSensitive)
                        value = kind == ContactPointKind.Email ? PartyIdentifiers.MaskEmail(value) : PartyIdentifiers.MaskPhone(value);
                    contacts.Add(new ContactPointDto(kind.ToString(), value, cr.GetBoolean(2), cr.GetBoolean(3)));
                }
            }

            AddressDto? address = null;
            await using (var addressCommand = new NpgsqlCommand("""
                SELECT country_code, province, city, postal_code, address_line FROM persons_addresses
                WHERE party_id=@id AND is_active ORDER BY is_primary DESC, address_id LIMIT 1;
                """, connection, tx))
            {
                addressCommand.Parameters.AddWithValue("id", id.Value);
                await using var ar = await addressCommand.ExecuteReaderAsync(cancellationToken);
                if (await ar.ReadAsync(cancellationToken))
                {
                    var postal = ar.GetString(3);
                    var line = ar.GetString(4);
                    address = includeSensitive
                        ? new AddressDto(ar.GetString(0), ar.GetString(1), ar.GetString(2), postal, line)
                        : new AddressDto(ar.GetString(0), ar.GetString(1), ar.GetString(2), PartyIdentifiers.MaskIdentifier(postal), "***");
                }
            }

            return new PartyDetails(id.ToString(), type, display, roles,
                includeSensitive || national is null ? national : PartyIdentifiers.MaskIdentifier(national),
                includeSensitive ? economic : null,
                includeSensitive || responsible is null ? responsible : PartyIdentifiers.MaskPersonName(responsible),
                contacts, address,
                includeSensitive ? source : PartyAcquaintanceSource.Unknown,
                includeSensitive ? note : null,
                includeSensitive ? preferences : null,
                created, includeSensitive, workClassification);
        }, cancellationToken);

        if (details is not null)
            await AppendReadAuditAsync(id.Value, actor, includeSensitive, cancellationToken);
        return details;
    }

    private async Task AppendReadAuditAsync(Guid partyId, string actor, bool sensitive, CancellationToken cancellationToken)
    {
        // The caller may still hold a pooled connection in its ambient transaction. Use a
        // dedicated non-pooled connection so a small pool (including pool size 1) cannot deadlock.
        var auditConnectionString = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString)
        {
            Pooling = false
        }.ConnectionString;
        await using var connection = new NpgsqlConnection(auditConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var auditTransaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "INSERT INTO persons_access_audit (audit_id, party_id, actor, action, sensitive_included) VALUES (@audit, @party, @actor, 'read', @sensitive);",
            connection, auditTransaction);
        command.Parameters.AddWithValue("audit", Guid.CreateVersion7());
        command.Parameters.AddWithValue("party", partyId);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("sensitive", sensitive);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await auditTransaction.CommitAsync(cancellationToken);
    }

    private async Task<T> RunAsync<T>(
        DbTransaction? ambientTransaction,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work,
        CancellationToken cancellationToken)
    {
        try
        {
            if (ambientTransaction is not null)
            {
                var joined = ambientTransaction as NpgsqlTransaction
                    ?? throw new ArgumentException("The ambient transaction must be an NpgsqlTransaction.", nameof(ambientTransaction));
                var joinedConnection = joined.Connection
                    ?? throw new InvalidOperationException("The ambient transaction is no longer associated with a connection.");
                return await work(joinedConnection, joined);
            }

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var result = await work(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Never surface constraint or column names: they would confirm which identifier already exists.
            throw new DuplicatePartyIdentifierException();
        }
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);

    private static string RequireActor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException("A trusted authenticated actor identifier is required.", nameof(value));
        return value;
    }

    private static string? OptionalText(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = PersianText.Normalize(value);
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", name);
        return normalized.Length == 0 ? null : normalized;
    }

    private static void AddNullable(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.AddWithValue(name, (object?)value ?? DBNull.Value);
}
