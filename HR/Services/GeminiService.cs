using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;

namespace HR.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiService(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public async Task<ResumeAnalysis> AnalyzeAsync(string vacancy, string resume)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Не задано Gemini:ApiKey в appsettings.json.");
            }

            var client = new Client(null, null, _apiKey);

            var prompt = $$"""
                You are an experienced HR specialist.
                Compare the candidate's resume with the job vacancy.
                Write candidateExperience and candidateDescription in Ukrainian.
                Return ONLY valid JSON in this format:
                {
                  "candidateName": "",
                  "candidateSurname": "",
                  "skills": [],
                  "candidateMatchPercentage": 0,
                  "candidateExperience": "",
                  "candidateDescription": ""
                }

                ===== VACANCY =====
                {{vacancy}}

                ===== RESUME =====
                {{resume}}
                """;

            var response = await client.Models.GenerateContentAsync(
                model: _model,
                contents: prompt,
                new GenerateContentConfig { Temperature = 0.2 }
            );

            var text = (response.Candidates[0].Content.Parts[0].Text ?? "")
                .Replace("```json", "").Replace("```", "").Trim();

            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                string Str(string name) =>
                    root.TryGetProperty(name, out var v) ? v.ToString() : string.Empty;

                var skills = new List<string>();
                if (root.TryGetProperty("skills", out var s) && s.ValueKind == JsonValueKind.Array)
                {
                    skills = s.EnumerateArray().Select(x => x.ToString())
                        .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                }

                int? pct = null;
                if (double.TryParse(Str("candidateMatchPercentage").Trim().TrimEnd('%'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var p))
                {
                    pct = (int)Math.Round(p);
                }

                return new ResumeAnalysis
                {
                    CandidateName = $"{Str("candidateName")} {Str("candidateSurname")}".Trim(),
                    MatchPercentage = pct,
                    Experience = Str("candidateExperience"),
                    Description = Str("candidateDescription"),
                    Skills = skills
                };
            }
            catch (JsonException)
            {
                return new ResumeAnalysis { Description = text };
            }
        }
    }
}
