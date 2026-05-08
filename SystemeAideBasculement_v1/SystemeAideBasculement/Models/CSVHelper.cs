namespace SystemeAideBasculement.Models
{
    public class CSVHelper
    {
        public string ConvertToCSV(IEnumerable<string> values)
        {
            if (values == null || !values.Any())
            {
                return string.Empty;
            }

            var sorted = values.OrderBy(h => h, StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", sorted);
        }

        public static List<string> SplitCsvOrdered(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, EndpointValue.None, StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string JoinCsvOrdered(IEnumerable<string> values)
        {
            return string.Join(", ",
                values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

    }
}
