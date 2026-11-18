using Xunit;
namespace MDGsm.Domain.Tests.IdentityAccess;
public sealed class BootstrapInvariantTests
{
 [Fact] public void FixedPairIsAbsent()
 {
  var marker = new string(new[] { (char)97,(char)100,(char)109,(char)105,(char)110,'/',(char)97,(char)100,(char)109,(char)105,(char)110 });
  var root = Directory.GetCurrentDirectory();
  while (!File.Exists(Path.Combine(root,"MDGsm.sln")) && Directory.GetParent(root) is not null) root = Directory.GetParent(root)!.FullName;
  foreach (var folder in new[] { "src", "installer", "database", "tools", "scripts" })
   if (Directory.Exists(Path.Combine(root,folder)))
    foreach (var path in Directory.GetFiles(Path.Combine(root,folder),"*",SearchOption.AllDirectories))
     if (!path.EndsWith(".dll") && !path.EndsWith(".exe")) Assert.DoesNotContain(marker,File.ReadAllText(path),StringComparison.OrdinalIgnoreCase);
 }
}