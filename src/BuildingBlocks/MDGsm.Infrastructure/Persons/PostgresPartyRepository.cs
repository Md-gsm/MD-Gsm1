using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using Npgsql;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// PostgreSQL implementation for Persons registration and read operations.
/// Writes are parameterized and atomic; actor identity must come from the trusted API boundary.
/// </summary>
public sealed class PostgresPartyRepository(NpgsqlDataSource dataSource) : IPartyRepository
{
    public Task<string> AddNaturalAsync(RegisterNaturalParty request, string actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PartyRegistrationRules.ValidateRoles(request.Roles);
        var party = NaturalPerson.Create(request.GivenName, request.FamilyName, request.Roles);
        var nationalId = PartyIdentifiers.NormalizeIranianNationalId(request.NationalId);
        return InsertAsync(party.Id, PartyType.NaturalPerson, request.Roles,
            party.GivenName, party.FamilyName, null, nationalId, null, null, null,
            request, actorId, cancellationToken);
    }

    public Task<string> AddLegalAsync(RegisterLegalParty request, string actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PartyRegistrationRules.ValidateRoles(request.Roles);
        var party = LegalPerson.Create(request.LegalName, request.Roles);
        var legalNationalId = PartyIdentifiers.NormalizeIranianLegalNationalId(request.LegalNationalId);
        var economicCode = PartyIdentifiers.NormalizeEconomicCode(request.EconomicCode);
        var responsible = OptionalText(request.ResponsiblePersonName, 200, nameof(request.ResponsiblePersonName));
        return InsertAsync(party.Id, PartyType.LegalPerson, request.Roles,
            null, null, party.LegalName, null, legalNationalId, economicCode, responsible,
            request, actorId, cancellationToken);
    }

    private async Task<string> InsertAsync(
        PartyId partyId, PartyType partyType, PartyBusinessRoles roles,
        string? givenName, string? familyName, string? legalName,
        string? nationalId, string? legalNationalId, string? economicCode, string? responsible,
        IPartyRegistrationCommon request, string actorId, CancellationToken cancellationToken)
    {
        var actor = RequireActor(actorId);
        if (!Enum.IsDefined(request.AcquaintanceSource))
            throw new ArgumentOutOfRangeException(nameof(request.AcquaintanceSource));
        ArgumentNullException.ThrowIfNull(request.CommunicationPreferences);
        var note = PartyRegistrationRules.RequireInternalNote(request.InternalNote);
        var financial = PartyFinancialProfile.Create(request.CreditLimitRial, request.DefaultDiscountPercent,
            request.DefaultPaymentMethod, request.BankCardLastFour);
        var primaryMobile = PartyContactPoint.Mobile(request.PrimaryMobile, true,
            request.CommunicationPreferences.RepairStatusSmsEnabled || request.CommunicationPreferences.ReadyForDeliverySmsEnabled);
        var contacts = new List<PartyContactPoint> { primaryMobile };
        if (!string.IsNullOrWhiteSpace(request.AlternateMobile)) contacts.Add(PartyContactPoint.Mobile(request.AlternateMobile));
        if (!string.IsNullOrWhiteSpace(request.Landline)) contacts.Add(PartyContactPoint.Landline(request.Landline));
        if (!string.IsNullOrWhiteSpace(request.Email)) contacts.Add(PartyContactPoint.Email(request.Email));
        if (contacts.Where(x => x.Kind == ContactPointKind.Mobile).Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() !=
            contacts.Count(x => x.Kind == ContactPointKind.Mobile))
            throw new ArgumentException("Primary and alternate mobile numbers must be different.");

        PartyAddress? address = null;
        var addressFields = new[] { request.Province, request.City, request.PostalCode, request.AddressLine };
        if (addressFields.Any(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (addressFields.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Province, city, postal code and full address must be supplied together.");
            address = PartyAddress.Create(request.Province!, request.City!, request.PostalCode!, request.AddressLine!);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand("""
            INSERT INTO persons_parties
              (party_id, party_type, business_roles, given_name, family_name, legal_name,
               national_id, legal_national_id, economic_code, responsible_person_name,
               acquaintance_source, internal_note, credit_limit_rial, default_discount_percent,
               default_payment_method, bank_card_last_four, repair_status_sms_enabled,
               ready_for_delivery_sms_enabled, promotions_sms_enabled, created_by, updated_by)
            VALUES
              (@id, @type, @roles, @given, @family, @legal, @national, @legalNational, @economic, @responsible,
               @source, @note, @credit, @discount, @payment, @lastFour, @repairSms, @readySms, @promoSms, @actor, @actor);
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
            command.Parameters.AddWithValue("credit", financial.CreditLimitRial);
            command.Parameters.AddWithValue("discount", financial.DefaultDiscountPercent);
            command.Parameters.AddWithValue("payment", (short)financial.DefaultPaymentMethod);
            AddNullable(command, "lastFour", financial.BankCardLastFour);
            command.Parameters.AddWithValue("repairSms", request.CommunicationPreferences.RepairStatusSmsEnabled);
            command.Parameters.AddWithValue("readySms", request.CommunicationPreferences.ReadyForDeliverySmsEnabled);
            command.Parameters.AddWithValue("promoSms", request.CommunicationPreferences.PromotionsSmsEnabled);
            command.Parameters.AddWithValue("actor", actor);
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

        await transaction.CommitAsync(cancellationToken);
        return partyId.ToString();
    }

    public async Task<IReadOnlyList<PartyListItem>> SearchAsync(string? query, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var term = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT p.party_id, p.party_type, p.business_roles,
                   CASE WHEN p.party_type = 1 THEN concat(p.given_name, ' ', p.family_name) ELSE p.legal_name END,
                   (SELECT c.value FROM persons_contact_points c WHERE c.party_id=p.party_id AND c.kind=1 AND c.is_primary LIMIT 1),
                   (SELECT a.city FROM persons_addresses a WHERE a.party_id=p.party_id AND a.is_primary LIMIT 1),
                   p.created_at_utc
            FROM persons_parties p
            WHERE p.is_active AND (
              @term IS NULL OR
              (p.party_type=1 AND concat_ws(' ', p.given_name, p.family_name) ILIKE '%' || @term || '%') OR
              (p.party_type=2 AND p.legal_name ILIKE '%' || @term || '%') OR
              EXISTS (SELECT 1 FROM persons_contact_points c WHERE c.party_id=p.party_id AND c.value ILIKE '%' || @term || '%')
            )
            ORDER BY p.created_at_utc DESC, p.party_id LIMIT @limit;
            """, connection);
        command.Parameters.AddWithValue("term", (object?)term ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);
        var results = new List<PartyListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(new PartyListItem(reader.GetGuid(0).ToString("D"), (PartyType)reader.GetInt16(1),
                reader.GetString(3), (PartyBusinessRoles)reader.GetInt16(2),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        return results;
    }

    public async Task<PartyDetails?> GetAsync(string partyId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(partyId, out var id) || id == Guid.Empty) return null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT party_type, business_roles, given_name, family_name, legal_name, national_id,
                   legal_national_id, economic_code, responsible_person_name, acquaintance_source,
                   internal_note, credit_limit_rial, default_discount_percent, default_payment_method,
                   bank_card_last_four, repair_status_sms_enabled, ready_for_delivery_sms_enabled,
                   promotions_sms_enabled, created_at_utc
            FROM persons_parties WHERE party_id=@id AND is_active;
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var type = (PartyType)reader.GetInt16(0);
        var roles = (PartyBusinessRoles)reader.GetInt16(1);
        var display = type == PartyType.NaturalPerson ? $"{reader.GetString(2)} {reader.GetString(3)}" : reader.GetString(4);
        var national = type == PartyType.NaturalPerson
            ? (reader.IsDBNull(5) ? null : reader.GetString(5))
            : (reader.IsDBNull(6) ? null : reader.GetString(6));
        var economic = reader.IsDBNull(7) ? null : reader.GetString(7);
        var responsible = reader.IsDBNull(8) ? null : reader.GetString(8);
        var source = (PartyAcquaintanceSource)reader.GetInt16(9);
        var note = reader.GetString(10);
        var credit = reader.GetInt64(11);
        var discount = reader.GetDecimal(12);
        var payment = (DefaultPaymentMethod)reader.GetInt16(13);
        var lastFour = reader.IsDBNull(14) ? null : reader.GetString(14).Trim();
        var preferences = new PartyCommunicationPreferences(reader.GetBoolean(15), reader.GetBoolean(16), reader.GetBoolean(17));
        var created = reader.GetFieldValue<DateTimeOffset>(18);
        await reader.CloseAsync();

        var contacts = new List<ContactPointDto>();
        await using (var contactCommand = new NpgsqlCommand("""
            SELECT kind, value, is_primary, is_messaging_allowed FROM persons_contact_points
            WHERE party_id=@id ORDER BY is_primary DESC, kind, contact_point_id;
            """, connection))
        {
            contactCommand.Parameters.AddWithValue("id", id);
            await using var cr = await contactCommand.ExecuteReaderAsync(cancellationToken);
            while (await cr.ReadAsync(cancellationToken))
                contacts.Add(new ContactPointDto(((ContactPointKind)cr.GetInt16(0)).ToString(), cr.GetString(1), cr.GetBoolean(2), cr.GetBoolean(3)));
        }

        AddressDto? address = null;
        await using (var addressCommand = new NpgsqlCommand("""
            SELECT country_code, province, city, postal_code, address_line FROM persons_addresses
            WHERE party_id=@id ORDER BY is_primary DESC, address_id LIMIT 1;
            """, connection))
        {
            addressCommand.Parameters.AddWithValue("id", id);
            await using var ar = await addressCommand.ExecuteReaderAsync(cancellationToken);
            if (await ar.ReadAsync(cancellationToken))
                address = new AddressDto(ar.GetString(0), ar.GetString(1), ar.GetString(2), ar.GetString(3), ar.GetString(4));
        }

        return new PartyDetails(id.ToString("D"), type, display, roles, national, economic, responsible,
            contacts, address, credit, discount, payment, lastFour, source, note, preferences, created);
    }

    private static string RequireActor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException("A trusted authenticated actor identifier is required.", nameof(value));
        return value;
    }

    private static string? OptionalText(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", name);
        return normalized;
    }

    private static void AddNullable(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.AddWithValue(name, (object?)value ?? DBNull.Value);
}
