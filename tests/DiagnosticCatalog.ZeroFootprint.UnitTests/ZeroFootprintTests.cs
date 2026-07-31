using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Xunit;

namespace DiagnosticCatalog.ZeroFootprint.UnitTests;

/// <summary>
/// Marks a member so a test can prove the compiler emitted attributes on it at all.
/// </summary>
/// <remarks>
/// Without this, "the suppression is absent" is indistinguishable from "nothing was emitted", and the
/// assertion that matters here would pass for the wrong reason forever — the characteristic way a
/// negative test rots.
/// </remarks>
[AttributeUsage(AttributeTargets.All)]
internal sealed class PresenceMarkerAttribute : Attribute
{
}

/// <summary>
/// The negative half of §21.5: what a consumer's ordinary build actually carries.
/// </summary>
/// <remarks>
/// <para>
/// This assembly does not define <c>CODE_ANALYSIS</c>, so it compiles the way a consumer's does.
/// <c>SuppressMessageAttribute</c> is <c>[Conditional("CODE_ANALYSIS")]</c> (§3.4): the compiler omits
/// the call site entirely and folds the referenced constants away, so nothing of the suppression
/// survives into metadata. That is goal 12 of §4 — no runtime behaviour in the consuming application —
/// and it is a property of the platform, not of this library, which is precisely why it is worth
/// pinning: nothing here would fail if it stopped being true.
/// </para>
/// <para>
/// The sibling project asserts the opposite half against the same subject, and the two must be read
/// together. Neither is meaningful alone: one shows the values are readable when a tool asks for them,
/// the other that they cost the consumer nothing when no tool does.
/// </para>
/// </remarks>
public sealed class ZeroFootprintTests
{
    private static class TestRules
    {
        [DiagnosticRule]
        public static class TEST0001
        {
            public const string Id = nameof(TEST0001);

            public const string Category = "Usage";
        }

        /// <summary>A trim rule, whose suppression is the one the platform does keep (§9.1).</summary>
        [DiagnosticRule]
        public static class IL2026
        {
            public const string Id = nameof(IL2026);

            public const string Category = "Trimming";
        }
    }

    /// <summary>
    /// One member carrying both suppressions, as §3.4 describes the verification.
    /// </summary>
    /// <remarks>
    /// Both on the same member on purpose. The footprint question is settled per ATTRIBUTE rather than
    /// per library, so a subject carrying only one of them could not show the difference — and the
    /// unconditional one doubles as the proof that this member reached metadata at all.
    /// </remarks>
    [PresenceMarker]
    [SuppressMessage(
        TestRules.TEST0001.Category,
        TestRules.TEST0001.Id,
        Justification = "Subject of the zero-footprint test.")]
#if NET
    [UnconditionalSuppressMessage(
        TestRules.IL2026.Category,
        TestRules.IL2026.Id,
        Justification = "Subject of the zero-footprint test.")]
#endif
    private sealed class Subject
    {
    }

    [Fact]
    public void The_subject_did_reach_metadata()
    {
        // The control. Every assertion below is about something being ABSENT, and absence proves
        // nothing until the member is known to have been emitted with attributes of its own.
        Assert.NotNull(typeof(Subject).GetCustomAttribute<PresenceMarkerAttribute>());
    }

    [Fact]
    public void A_suppression_leaves_no_trace_in_a_consumer_s_build()
    {
        // The whole zero-footprint guarantee, in one assertion. The suppression above compiles, is
        // read by Roslyn out of the semantic model, and reaches the shipped assembly as nothing at
        // all — no attribute, no retained string, no reference to the rule type.
        Assert.Null(typeof(Subject).GetCustomAttribute<SuppressMessageAttribute>());
    }

    [Fact]
    public void The_rule_type_is_still_reachable_although_nothing_references_it()
    {
        // The constants were folded into a call site that was then removed, so the rule type is left
        // with no consumer in this assembly. It remains perfectly usable — which is what makes the
        // catalogue a compile-time construct rather than a runtime one.
        Assert.Equal("TEST0001", TestRules.TEST0001.Id);
        Assert.Equal("Usage", TestRules.TEST0001.Category);
    }

#if NET
    [Fact]
    public void An_unconditional_suppression_is_preserved_with_its_values_folded()
    {
        // The opposite behaviour, by design and on the same member: UnconditionalSuppressMessage
        // carries no [Conditional] precisely so ILLink can read it from the compiled assembly long
        // after the compiler has run (§9.1). The catalogue reference is gone; the literals it folded
        // to are what survives, and they are what ILLink matches on.
        UnconditionalSuppressMessageAttribute? attribute = typeof(Subject)
            .GetCustomAttribute<UnconditionalSuppressMessageAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("IL2026", attribute!.CheckId);
        Assert.Equal("Trimming", attribute.Category);
    }
#endif
}
