using Microsoft.EntityFrameworkCore;

namespace MDGsm.Infrastructure.Persistence;

/// <summary>Central EF Core context. Domain entities and schema migrations will be added in later, reviewed stages.</summary>
public sealed class MdGsmDbContext(DbContextOptions<MdGsmDbContext> options) : DbContext(options)
{
    // Intentionally contains no DbSets yet: stage 6 establishes infrastructure without inventing business schema.
}
