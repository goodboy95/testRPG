using Godot;

namespace TownRpg.Presentation;

public static class UiText
{
    public static string Get(string key, params object[] arguments)
    {
        var value = TranslationServer.Translate(key).ToString();
        if (value == key) throw new InvalidOperationException($"Missing UI translation: {key}");
        return arguments.Length == 0 ? value : string.Format(System.Globalization.CultureInfo.CurrentCulture, value, arguments);
    }
}
