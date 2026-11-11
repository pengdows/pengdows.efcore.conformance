namespace pengdows.efcore.conformance;

/// <summary>
///     Whether one provider implements one API. An API is implemented when its scenarios run: a Pass or a Fail means the provider has
///     it (a Fail is a correctness question). A scenario the provider declares unsupported, or skips for a capability it lacks, means the
///     API is not implemented there. Any other outcome is unknown, and is never counted as either.
/// </summary>
public enum ApiStatus
{
    Implemented,
    NotImplemented,
    Untranslatable,
    Unknown,
    NoScenario
}

public enum ContractResultStatus
{
    Pass,
    Fail,
    Unsupported,
    Blocked,
    NotTested
}

/// <summary>Counts over the APIs two providers both have a determinate status for (Implemented or NotImplemented).</summary>
public sealed record PairCounts(int Both, int OnlyA, int OnlyB, int Neither, int Excluded);

public static class CompletenessAnalysis
{
    /// <summary>Classifies a behavior result without allowing absence or skips to imply a pass.</summary>
    public static ContractResultStatus ContractStatusOf(IEnumerable<string> contractIds, IReadOnlyDictionary<string, ConformanceOutcome> outcomes)
    {
        var ids = contractIds.ToArray();
        if (ids.Length == 0 || ids.Any(id => !outcomes.ContainsKey(id)))
        {
            return ContractResultStatus.NotTested;
        }

        var results = ids.Select(id => outcomes[id]).ToArray();
        if (results.Any(result => result is ConformanceOutcome.Fail or ConformanceOutcome.Error))
        {
            return ContractResultStatus.Fail;
        }

        if (results.Any(result => result is ConformanceOutcome.UndeclaredCapability or ConformanceOutcome.Inconclusive))
        {
            return ContractResultStatus.Blocked;
        }

        if (results.Any(result => result is ConformanceOutcome.Unsupported or ConformanceOutcome.Skip or ConformanceOutcome.Untranslatable))
        {
            return ContractResultStatus.Unsupported;
        }

        return results.All(result => result == ConformanceOutcome.Pass) ? ContractResultStatus.Pass : ContractResultStatus.NotTested;
    }

    public static ApiStatus StatusOf(IEnumerable<string> contractIds, IReadOnlyDictionary<string, ConformanceOutcome> outcomes)
    {
        var ids = contractIds.ToList();
        if (ids.Count == 0)
        {
            return ApiStatus.NoScenario;
        }

        var seen = ids.Where(outcomes.ContainsKey).Select(id => outcomes[id]).ToList();
        if (seen.Count < ids.Count || seen.Any(outcome => outcome is ConformanceOutcome.Error or ConformanceOutcome.Inconclusive or ConformanceOutcome.UndeclaredCapability))
        {
            return ApiStatus.Unknown;
        }

        if (seen.Any(outcome => outcome is ConformanceOutcome.Unsupported or ConformanceOutcome.Skip))
        {
            return ApiStatus.NotImplemented;
        }

        return seen.Any(outcome => outcome == ConformanceOutcome.Untranslatable)
            ? ApiStatus.Untranslatable
            : ApiStatus.Implemented;
    }

    public static PairCounts Pair(IEnumerable<(ApiStatus A, ApiStatus B)> pairs)
    {
        int both = 0, onlyA = 0, onlyB = 0, neither = 0, excluded = 0;
        foreach (var (a, b) in pairs)
        {
            if (!Determinate(a) || !Determinate(b))
            {
                excluded++;
                continue;
            }

            switch (a == ApiStatus.Implemented, b == ApiStatus.Implemented)
            {
                case (true, true): both++; break;
                case (true, false): onlyA++; break;
                case (false, true): onlyB++; break;
                default: neither++; break;
            }
        }

        return new PairCounts(both, onlyA, onlyB, neither, excluded);
    }

    private static bool Determinate(ApiStatus status)
        => status is ApiStatus.Implemented or ApiStatus.NotImplemented;
}
