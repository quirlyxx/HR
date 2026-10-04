using HR.Models;
using HR.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR.Controllers
{
    public class AnalysisController : Controller
    {
        private const int MaxChars = 30000;
        private readonly GeminiService _gemini;

        public AnalysisController(GeminiService gemini)
        {
            _gemini = gemini;
        }

        [HttpGet]
        public IActionResult Index() => View(new AnalysisViewModel());

        [HttpPost]
        public async Task<IActionResult> Index(AnalysisViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Vacancy) || string.IsNullOrWhiteSpace(model.Resume))
            {
                model.Error = "Додайте і вакансію, і резюме.";
                return View(model);
            }

            if (model.Vacancy.Length > MaxChars || model.Resume.Length > MaxChars)
            {
                model.Error = "Текст завеликий (максимум 30 000 символів).";
                return View(model);
            }

            try
            {
                model.Result = await _gemini.AnalyzeAsync(model.Vacancy, model.Resume);
            }
            catch (Exception ex)
            {
                model.Error = "Не вдалося отримати аналіз: " + ex.Message;
            }

            return View(model);
        }
    }
}
