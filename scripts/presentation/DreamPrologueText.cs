using Godot;
using TownRpg.Core.Domain;

namespace TownRpg.Presentation;

public static class DreamPrologueText
{
    public static string Format(string key, params object[] values) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), values);
    public static string Get(string key)
    {
        var text = TranslationServer.Translate(key).ToString();
        if (text == key) throw new InvalidDataException($"Missing prologue localization: {key}");
        return text;
    }
    public static string Clue(string id) => Get(DreamPrologueCatalog.TextKey(id));
    public static string Puzzle(string id) => Get(DreamPrologueCatalog.TextKey(id) + "_NAME");
    public static string Prompt(string id) => Get(DreamPrologueCatalog.TextKey(id) + "_PROMPT");
    public static string Hint(string key) => Get(key);
}
