namespace BrightnessTray;

/// <summary>Sonnenauf-/untergang (NOAA/Wikipedia "Sunrise equation"), Ergebnis in lokaler Zeit.</summary>
internal static class Sun
{
    public static (DateTime? Rise, DateTime? Set) Times(DateTime date, double lat, double lon)
    {
        double n = (date.Date - new DateTime(2000, 1, 1)).Days;
        double jStar = n - lon / 360.0;                         // Länge: Ost = positiv
        double m = Mod(357.5291 + 0.98560028 * jStar, 360);
        double mr = Rad(m);
        double c = 1.9148 * Math.Sin(mr) + 0.02 * Math.Sin(2 * mr) + 0.0003 * Math.Sin(3 * mr);
        double lambda = Mod(m + c + 180 + 102.9372, 360);
        double lr = Rad(lambda);
        double jTransit = 2451545.0 + jStar + 0.0053 * Math.Sin(mr) - 0.0069 * Math.Sin(2 * lr);
        double sinDec = Math.Sin(lr) * Math.Sin(Rad(23.4397));
        double cosDec = Math.Cos(Math.Asin(sinDec));
        double cosW = (Math.Sin(Rad(-0.833)) - Math.Sin(Rad(lat)) * sinDec) / (Math.Cos(Rad(lat)) * cosDec);
        if (cosW > 1 || cosW < -1) return (null, null);        // Polartag / Polarnacht
        double w = Math.Acos(cosW) * 180 / Math.PI;
        return (FromJd(jTransit - w / 360.0), FromJd(jTransit + w / 360.0));
    }

    private static double Rad(double deg) => deg * Math.PI / 180.0;
    private static double Mod(double a, double m) => ((a % m) + m) % m;

    private static DateTime FromJd(double jd) =>
        DateTime.SpecifyKind(DateTime.FromOADate(jd - 2415018.5), DateTimeKind.Utc).ToLocalTime();
}
