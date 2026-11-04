using MDGsm.IdentityAccess.Application;
using Microsoft.EntityFrameworkCore;
namespace MDGsm.Infrastructure.Persistence;

/// <summary>Owns one serializable transaction and serializes capacity decisions on the singleton policy row.</summary>
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
    await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,cancellationToken);
    try
    {
     // Missing singleton row is a hard failure: never proceed without capacity serialization.
     _=await db.IdentitySystemSettings.FromSqlRaw("SELECT * FROM identity.system_settings WHERE settings_id = 1 FOR UPDATE").SingleAsync(cancellationToken);
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