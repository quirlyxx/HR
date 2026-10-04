using HR.Services;

namespace HR.Models
{
    public class AnalysisViewModel
    {
        public string? Vacancy { get; set; }
        public string? Resume { get; set; }
        public ResumeAnalysis? Result { get; set; }
        public string? Error { get; set; }
    }
}
