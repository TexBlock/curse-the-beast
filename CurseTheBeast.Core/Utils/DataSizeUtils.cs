namespace CurseTheBeast.Core.Utils;


public static class DataSizeUtils
{
    public static string Humanize(double size, double total)
    {
        if (total < 768)
            return $"{size}B";
        if (total < 768 * 1024)
            return $"{size / 1024:0.#}K";

        return $"{size / 1024 / 1024:0.#}M";
    }
}
