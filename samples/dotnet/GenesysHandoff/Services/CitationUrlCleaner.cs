using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Utility class for removing citation URLs and references from text content.
    /// </summary>
    public static class CitationUrlCleaner
    {
        private static readonly string[] CitationHeaders = ["sources:", "references:", "citations:"];
        private static readonly Regex CitationLinePattern = new(@"^\s*(\[\d+\]:|\d+[\.\)]|-\s*)\s*https?://", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Removes citation URLs and reference lines from the end of the text.
        /// </summary>
        public static string RemoveCitationUrlsFromTail(string text, IList<Entity>? entities)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var citationUrls = ExtractCitationUrls(entities);
            if (citationUrls.Count == 0)
                return text;

            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            TrimTrailingBlankLines(lines);

            int originalCount = lines.Count;
            RemoveCitationLinesFromTail(lines, citationUrls);
            TrimTrailingBlankLines(lines);

            return lines.Count < originalCount ? string.Join("\n", lines) : text;
        }

        private static HashSet<string> ExtractCitationUrls(IList<Entity>? entities)
        {
            if (entities == null || entities.Count == 0)
                return [];

            return entities
                .OfType<AIEntity>()
                .Where(e => e.Citation != null)
                .SelectMany(e => e.Citation)
                .Select(c => c.Appearance?.Url)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        }

        private static void RemoveCitationLinesFromTail(List<string> lines, HashSet<string> citationUrls)
        {
            while (lines.Count > 0)
            {
                var lastLine = lines[^1].Trim();

                if (IsCitationLine(lastLine, citationUrls))
                {
                    lines.RemoveAt(lines.Count - 1);
                }
                else
                {
                    break;
                }
            }
        }

        private static bool IsCitationLine(string line, HashSet<string> citationUrls)
        {
            if (string.IsNullOrWhiteSpace(line))
                return true;

            if (CitationHeaders.Any(h => line.Equals(h, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (citationUrls.Any(url => line.Contains(url, StringComparison.OrdinalIgnoreCase)))
                return true;

            return CitationLinePattern.IsMatch(line);
        }

        private static void TrimTrailingBlankLines(List<string> lines)
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }
    }
}
