using Spectre.Console;

namespace CurseTheBeast.Core;

public static class GlobalStyle
{
    public static readonly StyleWrap Success = new("green");
    public static readonly StyleWrap Focused = new("yellow bold");
    public static readonly StyleWrap Notice = new("blue");
    public static readonly StyleWrap Normal = new("white");
    public static readonly StyleWrap Error = new("red");
    public static readonly StyleWrap Low = new("grey37");
    public static readonly StyleWrap Shallow = new("grey58");

    public class StyleWrap
    {
        public string Key { get; }
        readonly Style _style;

        public StyleWrap(string key)
        {
            Key = key;
            _style = Style.Parse(key);
        }

        public static implicit operator Style(StyleWrap wrap) => wrap._style;

        public string Text(string text)
        {
            return $"[{Key}]{Spectre.Console.Markup.Escape(text)}[/]";
        }

        public Markup Markup(string text)
        {
            return new Markup(Text(text));
        }

        public void WriteLine(string text)
        {
            AnsiConsole.MarkupLine(Text(text));
        }

        public void WriteLine()
        {
            AnsiConsole.WriteLine();
        }

        public void Write(string text)
        {
            AnsiConsole.Markup(Text(text));
        }
    }
}
