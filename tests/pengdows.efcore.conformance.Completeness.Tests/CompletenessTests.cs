using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Completeness.Tests;

/// <summary>
///     An API is implemented on a provider when its scenarios run: a Pass or a Fail means the provider has it (a Fail is a
///     correctness question, not a completeness one). A scenario the provider declares unsupported, or skips for a capability it
///     lacks, means the API is not implemented there. Any other outcome is unknown and never counted as either.
/// </summary>
public class CompletenessTests
{
    private static readonly string[] Ids = ["A-SYNC", "A-ASYNC"];

    [Fact]
    public void Missing_behavior_result_is_not_tested_and_never_passes()
    {
        Assert.Equal(ContractResultStatus.NotTested, CompletenessAnalysis.ContractStatusOf([], new Dictionary<string, ConformanceOutcome>()));
        Assert.Equal(ContractResultStatus.NotTested, CompletenessAnalysis.ContractStatusOf(["contract"], new Dictionary<string, ConformanceOutcome>()));
    }

    [Fact]
    public void Declared_unsupported_is_not_a_pass_and_does_not_hide_a_failure()
    {
        Assert.Equal(ContractResultStatus.Unsupported, CompletenessAnalysis.ContractStatusOf(["contract"], new Dictionary<string, ConformanceOutcome> { ["contract"] = ConformanceOutcome.Unsupported }));
        Assert.Equal(ContractResultStatus.Fail, CompletenessAnalysis.ContractStatusOf(["contract"], new Dictionary<string, ConformanceOutcome> { ["contract"] = ConformanceOutcome.Fail }));
    }

    [Fact]
    public void Only_explicitly_passing_executed_results_pass_a_behavior_contract()
    {
        Assert.Equal(ContractResultStatus.Pass, CompletenessAnalysis.ContractStatusOf(["sync", "async"], new Dictionary<string, ConformanceOutcome>
        {
            ["sync"] = ConformanceOutcome.Pass,
            ["async"] = ConformanceOutcome.Pass,
        }));
        Assert.Equal(ContractResultStatus.Blocked, CompletenessAnalysis.ContractStatusOf(["contract"], new Dictionary<string, ConformanceOutcome> { ["contract"] = ConformanceOutcome.UndeclaredCapability }));
    }

    [Fact]
    public void An_api_with_no_scenario_is_no_scenario()
    {
        Assert.Equal(ApiStatus.NoScenario, CompletenessAnalysis.StatusOf([], new Dictionary<string, ConformanceOutcome>()));
    }

    [Fact]
    public void Pass_and_fail_are_both_implemented()
    {
        var outcomes = new Dictionary<string, ConformanceOutcome> { ["A-SYNC"] = ConformanceOutcome.Pass, ["A-ASYNC"] = ConformanceOutcome.Fail };

        Assert.Equal(ApiStatus.Implemented, CompletenessAnalysis.StatusOf(Ids, outcomes));
    }

    [Fact]
    public void A_declared_unsupported_or_skipped_scenario_is_not_implemented()
    {
        var unsupported = new Dictionary<string, ConformanceOutcome> { ["A-SYNC"] = ConformanceOutcome.Pass, ["A-ASYNC"] = ConformanceOutcome.Unsupported };
        var skipped = new Dictionary<string, ConformanceOutcome> { ["A-SYNC"] = ConformanceOutcome.Pass, ["A-ASYNC"] = ConformanceOutcome.Skip };

        Assert.Equal(ApiStatus.NotImplemented, CompletenessAnalysis.StatusOf(Ids, unsupported));
        Assert.Equal(ApiStatus.NotImplemented, CompletenessAnalysis.StatusOf(Ids, skipped));
    }

    [Fact]
    public void A_scenario_missing_from_the_run_or_erroring_is_unknown_not_implemented()
    {
        var missing = new Dictionary<string, ConformanceOutcome> { ["A-SYNC"] = ConformanceOutcome.Pass };
        var errored = new Dictionary<string, ConformanceOutcome> { ["A-SYNC"] = ConformanceOutcome.Pass, ["A-ASYNC"] = ConformanceOutcome.Error };

        Assert.Equal(ApiStatus.Unknown, CompletenessAnalysis.StatusOf(Ids, missing));
        Assert.Equal(ApiStatus.Unknown, CompletenessAnalysis.StatusOf(Ids, errored));
    }

    [Fact]
    public void Untranslatable_is_an_ef_limit_not_a_provider_gap()
    {
        var outcomes = new Dictionary<string, ConformanceOutcome> { ["A-SYNC"] = ConformanceOutcome.Pass, ["A-ASYNC"] = ConformanceOutcome.Untranslatable };

        Assert.Equal(ApiStatus.Untranslatable, CompletenessAnalysis.StatusOf(Ids, outcomes));
    }

    [Fact]
    public void Pairwise_counts_split_the_shared_surface_into_both_only_x_only_y_and_neither()
    {
        var pairs = new (ApiStatus A, ApiStatus B)[]
        {
            (ApiStatus.Implemented, ApiStatus.Implemented),
            (ApiStatus.Implemented, ApiStatus.Implemented),
            (ApiStatus.Implemented, ApiStatus.NotImplemented),
            (ApiStatus.NotImplemented, ApiStatus.Implemented),
            (ApiStatus.NotImplemented, ApiStatus.NotImplemented),
            (ApiStatus.Implemented, ApiStatus.Unknown),
            (ApiStatus.NoScenario, ApiStatus.Implemented),
        };

        var counts = CompletenessAnalysis.Pair(pairs);

        Assert.Equal(2, counts.Both);
        Assert.Equal(1, counts.OnlyA);
        Assert.Equal(1, counts.OnlyB);
        Assert.Equal(1, counts.Neither);
        Assert.Equal(2, counts.Excluded);
    }
}
