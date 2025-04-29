using CurseTheBeast.Api.FTB;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CurseTheBeast.Utils;


public static class ErrorUtils
{
    const string IndentPrefix = "  ";
    const int ExceptionMaxLines = 5;

    static readonly IReadOnlySet<Type> IgnoredInnerExceptionTypes = new HashSet<Type>()
    {
        typeof(OperationCanceledException),
        typeof(TimeoutException),
    };
    static readonly IReadOnlySet<Type> SimpleExceptionTypes = new HashSet<Type>()
    {
        typeof(FTBException),
        typeof(Exception),
    };

    public static void Handler(Exception ex)
    {
        AnsiConsole.WriteLine();
        if (ex is Spectre.Console.Cli.CommandAppException cliEx)
        {
            Error.WriteLine(cliEx.Message);
            return;
        }
        internalHandler(ex, false);
        AnsiConsole.WriteLine();
        if (!NativeUtils.IsRunningByDoubleClick.Value)
            Error.WriteLine("发生了错误");
    }

    static void internalHandler(Exception ex, bool isInner = false)
    {
        if (SimpleExceptionTypes.Contains(ex.GetType()))
        {
            if (isInner)
                Error.WriteLine(indent(ex.Message));
            else
                Error.WriteLine("× " + ex.Message);
            if (ex.InnerException != null)
                internalHandler(ex.InnerException, true);
        }
        else if (!isInner || !IgnoredInnerExceptionTypes.Any(t => ex.GetType().IsAssignableTo(t)))
        {
            if (isInner)
                Error.WriteLine(indent(getString(ex)));
            else
                Error.WriteLine(getString(ex));
        }
    }

    static string getString(Exception ex)
    {
        return string.Join(Environment.NewLine, ex.ToString()
            .Replace("\r\n", "\n")
            .Split("\n")
            .Take(ExceptionMaxLines));
    }

    static string indent(string str)
    {
        return IndentPrefix + str.ReplaceLineEndings(IndentPrefix + Environment.NewLine);
    }
}
