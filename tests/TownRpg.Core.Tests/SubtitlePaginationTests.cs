using System.Globalization;
using TownRpg.Presentation;

namespace TownRpg.Core.Tests;

public sealed class SubtitlePaginationTests
{
    private static float Measure(string value) => StringInfo.ParseCombiningCharacters(value).Length;

    [Fact]
    public void WrapsAtPunctuationWithoutDroppingOrRepeatingText()
    {
        const string text = "窗边有维护用的纸卡。先看看这里留下了什么。她停下来，望向门后的灯。";
        var pages = SubtitlePagination.Pages(text, 10, Measure);
        Assert.Equal(text, string.Concat(pages).Replace("\n", "", StringComparison.Ordinal));
        Assert.All(pages, page => Assert.InRange(page.Split('\n').Length, 1, 2));
        Assert.All(pages.SelectMany(page => page.Split('\n')), line =>
        {
            Assert.InRange(Measure(line), 1, 10);
            Assert.DoesNotContain(line[0], "，。！？；：、）》】」』〉");
            Assert.DoesNotContain(line[^1], "（《【「『〈");
        });
    }

    [Fact]
    public void KeepsCombiningMarksAndEmojiTogetherAndPreservesExplicitEmptyLines()
    {
        const string text = "甲e\u0301👩‍👩‍👧‍👦乙\n\n丙丁";
        var pages = SubtitlePagination.Pages(text, 2, Measure);
        Assert.Equal(new[] { "甲e\u0301\n👩‍👩‍👧‍👦乙", "\n丙丁" }, pages);
    }

    [Fact]
    public void EmptyTextAndSingleOverwideGraphemeStillTerminate()
    {
        Assert.Equal(new[] { "" }, SubtitlePagination.Pages("", 1, Measure));
        Assert.Equal(new[] { "甲\n乙" }, SubtitlePagination.Pages("甲乙", 1, _ => 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SubtitlePagination.Pages("甲", 0, Measure));
    }
}
