using HR.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR.Controllers
{
    public class ResumesController : Controller
    {
        private static readonly TimeSpan PresignedUrlLifetime = TimeSpan.FromMinutes(15);

        private readonly S3Service _s3Service;

        public ResumesController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }

        public async Task<IActionResult> Index()
        {
            List<ResumeInfo> resumes;

            try
            {
                resumes = await _s3Service.ListResumesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                TempData["ResumeError"] = "Не вдалося завантажити список резюме.";
                resumes = new List<ResumeInfo>();
            }

            return View(resumes);
        }

        public async Task<IActionResult> Open(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return NotFound();
            }

            try
            {
                var url = await _s3Service.GetResumePresignedUrlAsync(key, PresignedUrlLifetime);
                return Redirect(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                TempData["ResumeError"] = "Не вдалося відкрити резюме.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                TempData["ResumeError"] = "Некоректний файл резюме.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _s3Service.DeleteResumeAsync(key);
                TempData["ResumeMessage"] = "Резюме видалено.";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                TempData["ResumeError"] = "Не вдалося видалити резюме.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
