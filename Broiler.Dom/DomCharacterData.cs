using System;

namespace Broiler.Dom;

public abstract class DomCharacterData(DomNodeType nodeType, DomDocument ownerDocument, string data) : 
    DomNode(nodeType, ownerDocument)
{
    private string _data = data ?? throw new ArgumentNullException(nameof(data));

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
    public override string? NodeValue => _data;

    /// <summary>
    /// Returns a substring of the character data starting at <paramref name="offset"/> with
    /// at most <paramref name="count"/> code units.
    /// </summary>
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
    public void AppendData(string data) => ReplaceData(_data.Length, 0, data);

    /// <summary>
    /// Inserts the specified string at <paramref name="offset"/>.
    /// </summary>
    public void InsertData(int offset, string data) => ReplaceData(offset, 0, data);

    /// <summary>
    /// Deletes a range of character data starting at <paramref name="offset"/>.
    /// </summary>
    public void DeleteData(int offset, int count) => ReplaceData(offset, count, string.Empty);

    /// <summary>
    /// Replaces <paramref name="count"/> code units at <paramref name="offset"/> with the specified string.
    /// </summary>
    public void ReplaceData(int offset, int count, string data)
    {
        if (offset < 0 || offset > _data.Length || count < 0)
            throw DomException.IndexSize($"The offset {offset} or count {count} is out of bounds (length is {_data.Length}).");

        var clampedCount = (int)Math.Min((long)count, _data.Length - offset);
        var insert = data ?? string.Empty;
        Data = string.Concat(_data.AsSpan(0, offset), insert, _data.AsSpan(offset + clampedCount));
    }
}

public sealed class DomText : DomCharacterData
{
    internal DomText(DomDocument ownerDocument, string data)
        : base(DomNodeType.Text, ownerDocument, data)
    {
    }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomText(ownerDocument, Data);

    /// <summary>
    /// Splits this text node into two nodes at the specified <paramref name="offset"/>,
    /// keeping both in tree order as siblings (if connected).
    /// </summary>
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

public sealed class DomComment : DomCharacterData
{
    internal DomComment(DomDocument ownerDocument, string data)
        : base(DomNodeType.Comment, ownerDocument, data)
    {
    }

    internal override DomNode CloneShallow(DomDocument ownerDocument) =>
        new DomComment(ownerDocument, Data);
}
