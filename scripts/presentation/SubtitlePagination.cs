using System.Globalization;

namespace TownRpg.Presentation;

/// <summary>Presentation-only wrapping, measured by the font actually used by the subtitle view.</summary>
public static class SubtitlePagination
{
    private const string CannotStart = "，。！？；：、）》】」』〉〕］｝,.!?;:%…";
    private const string CannotEnd = "（《【「『〈〔［｛(";
    private const string BreakAfter = "，。！？；：、,.!?;: \t";

    public static IReadOnlyList<string> Pages(string text, float width, Func<string, float> measure)
    {
        if (width <= 0 || !float.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width));
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var elements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(paragraph);
            while (enumerator.MoveNext()) elements.Add(enumerator.GetTextElement());
            if (elements.Count == 0) { lines.Add(""); continue; }
            var start = 0;
            while (start < elements.Count)
            {
                var end = start + 1;
                while (end < elements.Count && measure(string.Concat(elements.Skip(start).Take(end - start + 1))) <= width) end++;
                if (end < elements.Count)
                {
                    // Prefer punctuation near the measured edge, rather than a much shorter sentence fragment.
                    var minimum = start + Math.Max(1, (int)((end - start) * .65f));
                    for (var candidate = end; candidate >= minimum; candidate--)
                    {
                        if (BreakAfter.Contains(elements[candidate - 1], StringComparison.Ordinal) &&
                            !CannotStart.Contains(elements[candidate], StringComparison.Ordinal))
                        { end = candidate; break; }
                    }
                    while (end > start + 1 && (CannotStart.Contains(elements[end], StringComparison.Ordinal) ||
                           CannotEnd.Contains(elements[end - 1], StringComparison.Ordinal))) end--;
                }
                lines.Add(string.Concat(elements.Skip(start).Take(end - start)));
                start = end;
            }
        }
        return lines.Chunk(2).Select(chunk => string.Join("\n", chunk)).ToArray();
    }
}
