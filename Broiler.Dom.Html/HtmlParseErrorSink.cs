using System;
using System.Collections.Generic;

namespace Broiler.Dom.Html;

/// <summary>
/// Where the tokenizer and the tree builder report parse errors: the diagnostics list of one parse,
/// and the input they are located in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Positions are in the input the tokenizer reads.</b> Input stream preprocessing turns each CRLF
/// into one LF before tokenizing, so an offset counts one character fewer per earlier CRLF than the
/// original text. The line and column are the stable answer: preprocessing never merges two lines or
/// moves a character within its line, so line <c>n</c>, column <c>c</c> is the same place in the file
/// the page came from.
/// </para>
/// <para>
/// <b>Lines are indexed on the first error, not before.</b> A document with no errors — nearly every
/// parse, and every parse that did not ask — pays nothing; one with errors pays one pass over the input.
/// </para>
/// </remarks>
internal sealed class HtmlParseErrorSink(ICollection<HtmlParseDiagnostic> diagnostics)
{
    private string _input = string.Empty;
    private List<int>? _lineStarts;

    /// <summary>Sets the preprocessed input the offsets refer to.</summary>
    public void Attach(string input)
    {
        _input = input;
        _lineStarts = null;
    }

    /// <summary>Reports the parse error <paramref name="code"/> at <paramref name="offset"/>.</summary>
    public void Report(string code, string message, int offset)
    {
        offset = Math.Clamp(offset, 0, _input.Length);
        var (line, column) = Locate(offset);
        diagnostics.Add(new HtmlParseDiagnostic(message, offset) { Code = code, Line = line, Column = column });
    }

    private (int Line, int Column) Locate(int offset)
    {
        if (_lineStarts is null)
        {
            _lineStarts = [0];
            for (var i = 0; i < _input.Length; i++)
            {
                if (_input[i] == '\n')
                    _lineStarts.Add(i + 1);
            }
        }

        var index = _lineStarts.BinarySearch(offset);
        if (index < 0)
            index = ~index - 1;

        return (index + 1, offset - _lineStarts[index] + 1);
    }
}
