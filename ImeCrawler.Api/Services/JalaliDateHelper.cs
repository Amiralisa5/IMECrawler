using System.Globalization;

namespace ImeCrawler.Api.Services;

public static class JalaliDateHelper
{
    private static readonly PersianCalendar PersianCalendar = new();

    /// <summary>
    /// Converts Jalali date string (yyyy/MM/dd) to Gregorian DateOnly
    /// </summary>
    public static DateOnly JalaliToGregorian(string jalali)
    {
        var parts = jalali.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            throw new ArgumentException("Invalid jalali date format. Expected yyyy/MM/dd", nameof(jalali));

        int jy = int.Parse(parts[0]);
        int jm = int.Parse(parts[1]);
        int jd = int.Parse(parts[2]);

        var dt = PersianCalendar.ToDateTime(jy, jm, jd, 0, 0, 0, 0);
        return DateOnly.FromDateTime(dt);
    }

    /// <summary>
    /// Converts Gregorian DateOnly to Jalali date string (yyyy/MM/dd)
    /// </summary>
    public static string GregorianToJalali(DateOnly gregorian)
    {
        var dt = gregorian.ToDateTime(TimeOnly.MinValue);
        int jy = PersianCalendar.GetYear(dt);
        int jm = PersianCalendar.GetMonth(dt);
        int jd = PersianCalendar.GetDayOfMonth(dt);
        return $"{jy}/{jm:D2}/{jd:D2}";
    }

    /// <summary>
    /// Gets today's date in Jalali format
    /// </summary>
    public static string TodayJalali()
    {
        return GregorianToJalali(DateOnly.FromDateTime(DateTime.Now));
    }

    /// <summary>
    /// Gets yesterday's date in Jalali format
    /// </summary>
    public static string YesterdayJalali()
    {
        return GregorianToJalali(DateOnly.FromDateTime(DateTime.Now.AddDays(-1)));
    }
}

