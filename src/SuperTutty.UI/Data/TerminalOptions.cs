namespace SuperTutty.UI.Data;

public sealed class TerminalOptions
{
    public int FontSize { get; set; } = 12;
    public int Scrollback { get; set; } = 8000;

    public bool CursorBlink { get; set; } = true;
    public string CursorStyle { get; set; } = "block"; // block | bar | underline

    public string? FontFamily { get; set; }
    public double? LineHeight { get; set; }
    public double? LetterSpacing { get; set; }

    public bool AllowTransparency { get; set; } = true;
    public bool DrawBoldTextInBrightColors { get; set; } = true;

    public bool CopyOnSelect { get; set; } = true;

    public bool RightClickSelectsWord { get; set; } = true;

    public bool RightClickPaste { get; set; } = true;

    public bool BracketedPasteMode { get; set; } = true;

    public bool HistoryEnabled { get; set; } = true;

    public int HistoryLimit { get; set; } = 200;

    public bool PersistHistory { get; set; } = true;

    public TerminalThemeOptions Theme { get; set; } = new();

    public object ToJsOptions(string? historyKey = null)
    {
        // anonymous-object payload for JS interop (keeps JS signature stable)
        return new
        {
            fontSize = FontSize,
            scrollback = Scrollback,
            cursorBlink = CursorBlink,
            cursorStyle = CursorStyle,
            fontFamily = FontFamily,
            lineHeight = LineHeight,
            letterSpacing = LetterSpacing,
            allowTransparency = AllowTransparency,
            drawBoldTextInBrightColors = DrawBoldTextInBrightColors,
            copyOnSelect = CopyOnSelect,
            rightClickSelectsWord = RightClickSelectsWord,
            rightClickPaste = RightClickPaste,
            bracketedPasteMode = BracketedPasteMode,
            historyEnabled = HistoryEnabled,
            historyLimit = HistoryLimit,
            persistHistory = PersistHistory,
            historyKey,
            theme = Theme.ToJsTheme()
        };
    }
}

public sealed class TerminalThemeOptions
{
    public string Background { get; set; } = "#000000";
    public string Foreground { get; set; } = "#EDEDED";
    public string Cursor { get; set; } = "#EDEDED";
    public string CursorAccent { get; set; } = "#000000";
    public string SelectionBackground { get; set; } = "rgba(255, 255, 255, 0.22)";

    // ANSI palette (optional overrides)
    public string? Black { get; set; }
    public string? Red { get; set; }
    public string? Green { get; set; }
    public string? Yellow { get; set; }
    public string? Blue { get; set; }
    public string? Magenta { get; set; }
    public string? Cyan { get; set; }
    public string? White { get; set; }
    public string? BrightBlack { get; set; }
    public string? BrightRed { get; set; }
    public string? BrightGreen { get; set; }
    public string? BrightYellow { get; set; }
    public string? BrightBlue { get; set; }
    public string? BrightMagenta { get; set; }
    public string? BrightCyan { get; set; }
    public string? BrightWhite { get; set; }

    public object ToJsTheme()
    {
        return new
        {
            background = Background,
            foreground = Foreground,
            cursor = Cursor,
            cursorAccent = CursorAccent,
            selectionBackground = SelectionBackground,
            black = Black,
            red = Red,
            green = Green,
            yellow = Yellow,
            blue = Blue,
            magenta = Magenta,
            cyan = Cyan,
            white = White,
            brightBlack = BrightBlack,
            brightRed = BrightRed,
            brightGreen = BrightGreen,
            brightYellow = BrightYellow,
            brightBlue = BrightBlue,
            brightMagenta = BrightMagenta,
            brightCyan = BrightCyan,
            brightWhite = BrightWhite
        };
    }
}
