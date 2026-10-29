using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RelationalTestCollection
{
    public const string Name = "EF10 relational provider tests";
}
