using System;
using System.Collections.Generic;

namespace Broiler.Dom;

/// <summary>
/// A registered mutation observer's options (DOM Standard §4.3
/// <c>MutationObserverInit</c>). Engine-neutral: the JavaScript
/// <c>MutationObserver</c> wrapper marshals these from the script-supplied init
/// dictionary, and consumers use <see cref="DomMutationObserverFilter"/> to decide
/// which <see cref="DomMutationRecord"/>s a registration observes.
/// </summary>
public sealed class DomMutationObserverOptions
{
    public bool ChildList { get; init; }

    public bool Attributes { get; init; }

    public bool AttributeOldValue { get; init; }

    public bool CharacterData { get; init; }

    public bool CharacterDataOldValue { get; init; }

    public bool Subtree { get; init; }

    /// <summary>
    /// When non-null, only attribute mutations whose local name is in this list are
    /// observed. A null list observes all attributes.
    /// </summary>
    public IReadOnlyList<string>? AttributeFilter { get; init; }
}

/// <summary>
/// Engine-neutral mutation-observer filtering (the gate of DOM Standard §4.3
/// "queue a mutation record"): decides whether an observer registered on a node
/// with a given set of options observes a mutation. The bridge subscribes to the
/// canonical mutation stream and only <em>adapts</em> the matched records into
/// JavaScript callback objects; the matching rules live here.
/// </summary>
public static class DomMutationObserverFilter
{
    /// <summary>
    /// Returns whether an observer registered on <paramref name="observedTarget"/>
    /// with <paramref name="options"/> observes <paramref name="record"/>. The record's
    /// target must be the observed node — or, with <see cref="DomMutationObserverOptions.Subtree"/>,
    /// a descendant of it — and the record's type must be enabled (with the attribute
    /// filter, when set, containing the mutated attribute's name).
    /// </summary>
    public static bool Matches(DomMutationRecord record, DomNode observedTarget, DomMutationObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observedTarget);
        ArgumentNullException.ThrowIfNull(options);

        if (!ReferenceEquals(record.Target, observedTarget) &&
            !(options.Subtree && record.Target.IsDescendantOf(observedTarget)))
        {
            return false;
        }

        return record.Type switch
        {
            DomMutationType.ChildList => options.ChildList,
            DomMutationType.CharacterData => options.CharacterData,
            DomMutationType.Attributes => options.Attributes &&
                (options.AttributeFilter is null || AttributeFilterContains(options.AttributeFilter, record.AttributeName)),
            _ => false,
        };
    }

    /// <summary>
    /// Whether the observer records the mutation's old value, per
    /// <see cref="DomMutationObserverOptions.AttributeOldValue"/> /
    /// <see cref="DomMutationObserverOptions.CharacterDataOldValue"/>.
    /// </summary>
    public static bool CapturesOldValue(DomMutationRecord record, DomMutationObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(options);

        return record.Type switch
        {
            DomMutationType.Attributes => options.AttributeOldValue,
            DomMutationType.CharacterData => options.CharacterDataOldValue,
            _ => false,
        };
    }

    private static bool AttributeFilterContains(IReadOnlyList<string> filter, string? attributeName)
    {
        if (attributeName is null)
            return false;
        for (var i = 0; i < filter.Count; i++)
        {
            if (string.Equals(filter[i], attributeName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
