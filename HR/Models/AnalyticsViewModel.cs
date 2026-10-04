using HR.Services;

namespace HR.Models
{
    public class AnalyticsViewModel
    {
        public List<ResumeInfo> Candidates { get; set; } = new();

        public int Total => Candidates.Count;
        public int Analyzed => Candidates.Count(c => c.Analysis != null);
        public int Pending => Total - Analyzed;

        private IEnumerable<int> Scores => Candidates
            .Where(c => c.Analysis?.MatchPercentage != null)
            .Select(c => c.Analysis!.MatchPercentage!.Value);

        public int? AverageMatch => Scores.Any() ? (int)Math.Round(Scores.Average()) : null;
        public int High => Scores.Count(p => p >= 70);
        public int Mid => Scores.Count(p => p >= 40 && p < 70);
        public int Low => Scores.Count(p => p < 40);

        public List<KeyValuePair<string, int>> TopSkills => Candidates
            .Where(c => c.Analysis != null)
            .SelectMany(c => c.Analysis!.Skills.Select(s => s.Trim())
                .Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Select(g => new KeyValuePair<string, int>(g.First(), g.Count()))
            .OrderByDescending(x => x.Value).ThenBy(x => x.Key)
            .Take(8).ToList();
    }
}
