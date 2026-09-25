using HR.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR.Controllers
{
    public class HomeController : Controller
    {
        private readonly S3Service _s3Service;

        public HomeController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SaveRequirements(
            string vacancyTitle,
            string requirementsText)
        {
            if (string.IsNullOrWhiteSpace(vacancyTitle) ||
                string.IsNullOrWhiteSpace(requirementsText))
            {
                TempData["SaveError"] = "Заповніть усі поля.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var content =
                    $"Вакансія: {vacancyTitle}\n\n" +
                    $"Вимоги:\n{requirementsText}";

                await _s3Service.UploadTextAsync(
                    "requirements.txt",
                    content
                );

                TempData["SavedMessage"] =
                    "Вимоги успішно збережені.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                TempData["SaveError"] =
                    "Помилка при збереженні файлу.";

                return RedirectToAction(nameof(Index));
            }
        }
    }
}