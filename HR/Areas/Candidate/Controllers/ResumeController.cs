using HR.Models;
using HR.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR.Areas.Candidate.Controllers
{
    [Area("Candidate")]
    public class ResumeController : Controller
    {
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;
        private const string PdfContentType = "application/pdf";

        private readonly S3Service _s3Service;

        public ResumeController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(MaxFileSizeBytes + 1024 * 1024)]
        public async Task<IActionResult> Upload(CandidateResumeViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.FullName) ||
                string.IsNullOrWhiteSpace(model.ContactInfo))
            {
                TempData["UploadError"] = "Заповніть ім'я та контактні дані.";
                return RedirectToAction(nameof(Index));
            }

            var file = model.ResumeFile;

            if (file == null || file.Length == 0)
            {
                TempData["UploadError"] = "Оберіть файл резюме у форматі PDF.";
                return RedirectToAction(nameof(Index));
            }

            if (file.Length > MaxFileSizeBytes)
            {
                TempData["UploadError"] = "Файл завеликий. Максимальний розмір 10 МБ.";
                return RedirectToAction(nameof(Index));
            }

            var hasPdfExtension = Path.GetExtension(file.FileName)
                .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

            var hasPdfContentType = string.Equals(
                file.ContentType,
                PdfContentType,
                StringComparison.OrdinalIgnoreCase
            );

            if (!hasPdfExtension || !hasPdfContentType)
            {
                TempData["UploadError"] = "Резюме повинно бути файлом у форматі PDF.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using var resumeStream = file.OpenReadStream();

                await _s3Service.UploadResumeAsync(
                    resumeStream,
                    model.FullName.Trim(),
                    model.ContactInfo.Trim(),
                    file.FileName
                );

                TempData["UploadSuccess"] = "Резюме успішно надіслано. Дякуємо!";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                TempData["UploadError"] = "Помилка при завантаженні файлу. Спробуйте ще раз.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
