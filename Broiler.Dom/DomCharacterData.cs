using System;

namespace Broiler.Dom;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: an offset or count from script reaches string slicing unchecked, so an ArgumentOutOfRangeException escapes instead of a DomException named IndexSizeError
// Broiler-Human:        PENDING
public abstract class DomCharacterData(DomNodeType nodeType, DomDocument ownerDocument, string data) : 
    DomNode(nodeType, ownerDocument)
{
    private string _data = data ?? throw new ArgumentNullException(nameof(data));

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a Data write that changes the value publishes no CharacterData mutation record or leaves an ancestor's TreeVersion unchanged
    // Broiler-Human:        PENDING
    public string Data
    {
        get => _data;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.Equals(_data, value, StringComparison.Ordinal))
                return;

            var oldValue = _data;
            _data = value;
            MarkChanged();
            OwnerDocument.PublishMutation(new DomMutationRecord(
                DomMutationType.CharacterData,
                this,
                OldValue: oldValue,
                NewValue: value));
        }
    }

    /// <summary>The length of the character data in UTF-16 code units.</summary>
    public int Length => _data.Length;

    /// <summary>The character data, exposed through the canonical <see cref="DomNode.NodeValue"/>.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public override string? NodeValue => _data;

    /// <summary>
    /// Returns a substring of the character data starting at <paramref name="offset"/> with
    /// at most <paramref name="count"/> code units.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an offset equal to the data length with a positive count throws instead of returning the empty string
    // Broiler-Human:        PENDING
    public string SubstringData(int offset, int count)
    {
        if (offset < 0 || offset > _data.Length || count < 0)
            throw DomException.IndexSize($"The offset {offset} or count {count} is out of bounds (length is {_data.Length}).");

        var clampedCount = (int)Math.Min((long)count, _data.Length - offset);
        return _data.Substring(offset, clampedCount);
    }

    /// <summary>
    /// Appends the specified string to the end of the character data.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public void AppendData(string data) => ReplaceData(_data.Length, 0, data);

    /// <summary>
    /// Inserts the specified string at <paramref name="offset"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public void InsertData(int offset, string data) => ReplaceData(offset, 0, data);

    /// <summary>
    /// Deletes a range of character data starting at <paramref name="offset"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public void DeleteData(int offset, int count) => ReplaceData(offset, count, string.Empty);

    /// <summary>
    /// Replaces <paramref name="count"/> code units at <paramref name="offset"/> with the specified string.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a count reaching past the end of the data throws or removes code units before the offset instead of deleting through the end
    // Broiler-Human:        PENDING
    public void ReplaceData(int offset, int count, string data)
    {
        if (offset < 0 || offset > _data.Length || count < 0)
            throw DomException.IndexSize($"The offset {offset} or count {count} is out of bounds (length is {_data.Length}).");

        var clampedCount = (int)Math.Min((long)count, _data.Length - offset);
        var insert = data ?? string.Empty;
        Data = string.Concat(_data.AsSpan(0, offset), insert, _data.AsSpan(offset + clampedCount));
    }
}

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: SplitText on a node with a parent leaves the returned node anywhere but immediately after the original
// Broiler-Human:        PENDING
public sealed class DomText : DomCharacterData
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal DomText(DomDocument ownerDocument, string data)
        : base(DomNodeType.Text, ownerDocument, data)
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomText(ownerDocument, Data);

    /// <summary>
    /// Splits this text node into two nodes at the specified <paramref name="offset"/>,
    /// keeping both in tree order as siblings (if connected).
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: after SplitText at offset k the original keeps code units past k, or the returned node's data is not exactly the remainder
    // Broiler-Human:        PENDING
    public DomText SplitText(int offset)
    {
        if (offset < 0 || offset > Length)
            throw DomException.IndexSize($"The offset {offset} is out of bounds (length is {Length}).");

        var count = Length - offset;
        var remainingData = SubstringData(offset, count);
        DeleteData(offset, count);

        var newNode = OwnerDocument.CreateTextNode(remainingData);
        if (ParentNode is { } parent)
            parent.InsertBefore(newNode, NextSibling);

        return newNode;
    }
}

// Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class DomComment : DomCharacterData
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal DomComment(DomDocument ownerDocument, string data)
        : base(DomNodeType.Comment, ownerDocument, data)
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomComment(ownerDocument, Data);
}
