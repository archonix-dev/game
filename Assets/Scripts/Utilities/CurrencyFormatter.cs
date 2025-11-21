public static class CurrencyFormatter
{
    private static readonly string[] UnitNames = { "bit", "byte", "kilobyte", "megabyte", "gigabyte" };
    private static readonly long[] UnitThresholds = BuildThresholds();

    public static string FormatBits(long amountBits)
    {
        if (amountBits <= 0)
        {
            return "0 bit";
        }

        int unitIndex = 0;

        for (int i = UnitThresholds.Length - 1; i >= 0; i--)
        {
            if (amountBits >= UnitThresholds[i])
            {
                unitIndex = i;
                break;
            }
        }

        long value = amountBits / UnitThresholds[unitIndex];
        if (value <= 0)
        {
            value = 1;
        }

        return $"{value} {UnitNames[unitIndex]}";
    }

    private static long[] BuildThresholds()
    {
        long[] thresholds = new long[UnitNames.Length];
        thresholds[0] = 1L; // bits are the base unit

        for (int i = 1; i < thresholds.Length; i++)
        {
            long multiplier = i == 1 ? 8L : 1024L;
            thresholds[i] = thresholds[i - 1] * multiplier;
        }

        return thresholds;
    }
}

