using HR.Models;
using HR.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR.Controllers
{
    public class HrAnalyticsController : Controller
    {
        private readonly S3Service _s3Service;

        public HrAnalyticsController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }

        public async Task<IActionResult> Index()
        {
            var candidates = new List<ResumeInfo>();
            try
            {
                candidates = await _s3Service.ListResumesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                TempData["AnalyticsError"] = "Не вдалося завантажити дані з S3.";
            }

            return View(new AnalyticsViewModel { Candidates = candidates });
        }

        public async Task<IActionResult> Details(string key)
        {
            try
            {
                var resume = await _s3Service.GetResumeAsync(key);
                return resume == null ? NotFound() : View(resume);
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }
    }
}
