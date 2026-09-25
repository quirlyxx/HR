using System.ComponentModel.DataAnnotations;

namespace HR.Models
{
    public class CandidateResumeViewModel
    {
        [Required(ErrorMessage = "Вкажіть ваше ім'я.")]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Вкажіть контактні дані.")]
        [StringLength(200)]
        public string ContactInfo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Оберіть файл резюме у форматі PDF.")]
        public IFormFile ResumeFile { get; set; } = null!;
    }
}
