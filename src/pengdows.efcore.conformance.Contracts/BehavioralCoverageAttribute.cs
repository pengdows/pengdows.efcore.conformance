namespace pengdows.efcore.conformance;

/// <summary>Links an observable behavioral scenario to public EF Core API member families.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BehavioralCoverageAttribute(string contractId, params string[] apiSelectors) : Attribute
{
    public string ContractId { get; } = contractId;

    public IReadOnlyList<string> ApiSelectors { get; } = apiSelectors;
}
