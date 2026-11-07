using MDGsm.IdentityAccess.Application;
using Microsoft.EntityFrameworkCore;
namespace MDGsm.Infrastructure.Persistence;

/// <summary>Owns one transaction and serializes capacity decisions with a row lock on the singleton policy row.</summary>
public sealed class IdentityAccessUnitOfWork(MdGsmDbContext db) : IIdentityAccessUnitOfWork
{
 private bool _running;
 public async Task<T> ExecuteAsync<T>(Func<CancellationToken,Task<T>> operation,CancellationToken cancellationToken)
 {
  ArgumentNullException.ThrowIfNull(operation);
  if(_running)throw new InvalidOperationException("Nested IdentityAccess units of work are not supported.");
  _running=true;
  try
  {
   var strategy=db.Database.CreateExecutionStrategy();
   return await strategy.ExecuteAsync(async()=>{
    db.ChangeTracker.Clear();
    await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,cancellationToken);
    try
    {
     // The first database statement takes a row lock on the singleton policy row.
     // READ COMMITTED is intentional: after waiting for another login transaction,
     // subsequent statements receive a fresh snapshot and count the sessions that
     // transaction committed. SERIALIZABLE took its snapshot before the lock wait,
     // causing avoidable 40001 failures under bursts of simultaneous logins.
     var lockedRows = await db.Database.ExecuteSqlRawAsync(
      "UPDATE identity.system_settings SET updated_at_utc = updated_at_utc WHERE settings_id = 1",
      cancellationToken);
     if (lockedRows != 1)
      throw new InvalidOperationException("The IdentityAccess singleton session-policy row is missing.");
     _=await db.IdentitySystemSettings.SingleAsync(x=>x.SettingsId==1,cancellationToken);
     var result=await operation(cancellationToken);
     await db.SaveChangesAsync(cancellationToken);
     await transaction.CommitAsync(cancellationToken);
     return result;
    }
    catch
    {
     await transaction.RollbackAsync(CancellationToken.None);
     db.ChangeTracker.Clear();
     throw;
    }
   });
  }
  finally{_running=false;}
 }
}