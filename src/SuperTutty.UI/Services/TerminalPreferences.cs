using System.Text.Json;
using Microsoft.Maui.Storage;
using SuperTutty.UI.Data;

namespace SuperTutty.UI.Services;

public static class TerminalPreferences
{
    private const string TerminalOptionsKey = "terminal_options";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static TerminalOptions LoadOptions()
    {
        try
        {
            var json = Preferences.Default.Get(TerminalOptionsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new TerminalOptions();
            }

            return JsonSerializer.Deserialize<TerminalOptions>(json, SerializerOptions) ?? new TerminalOptions();
        }
        catch
        {
            return new TerminalOptions();
        }
    }

    public static void SaveOptions(TerminalOptions options)
    {
        try
        {
            var json = JsonSerializer.Serialize(options ?? new TerminalOptions(), SerializerOptions);
            Preferences.Default.Set(TerminalOptionsKey, json);
        }
        catch
        {
            // ignore preference failures
        }
    }
}
