using System.ComponentModel.DataAnnotations;

namespace NewShadowGuard.Models.ViewModels
{
    public class CreateIncidentViewModel
    {
        [Required(ErrorMessage = "Название обязательно")]
        [Display(Name = "Название инцидента")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Описание обязательно")]
        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Критичность обязательна")]
        [Display(Name = "Критичность")]
        public string Severity { get; set; }

        [Display(Name = "Тенант")]
        public int? TenantId { get; set; }

        [Display(Name = "Связанный лог")]
        public int? LogId { get; set; }

        [Display(Name = "Актив")]
        public int? AssetId { get; set; }
    }
}