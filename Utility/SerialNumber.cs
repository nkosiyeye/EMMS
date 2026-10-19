using System.Text;

namespace EMMS.Utility
{
    /// <summary>
    /// Serial numbers are typed or scanned by the user, never generated for a RequiresSerial item.
    /// Scanners in keyboard-wedge mode can add CR/LF/TAB, and pasted lists carry stray spaces.
    /// </summary>
    public static class SerialNumber
    {
        /// <summary>Trims, drops control characters and collapses inner whitespace. Returns null when empty.</summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var sb = new StringBuilder(value.Length);
            bool pendingSpace = false;
            foreach (var ch in value.Trim())
            {
                if (char.IsWhiteSpace(ch)) { pendingSpace = true; continue; }
                if (char.IsControl(ch)) continue;
                if (pendingSpace && sb.Length > 0) sb.Append(' ');
                pendingSpace = false;
                sb.Append(ch);
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>Serials that appear more than once (case-insensitive, matching the SQL Server collation).</summary>
        public static IReadOnlyList<string> FindDuplicates(IEnumerable<string?> serials) =>
            serials
                .Select(Normalize)
                .Where(s => s != null)
                .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
    }
}
