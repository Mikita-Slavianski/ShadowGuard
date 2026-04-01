using System.ComponentModel.DataAnnotations;

namespace NewShadowGuard.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Неверный формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Имя обязательно")]
        [Display(Name = "Полное имя")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Пароль обязателен")]
        [MinLength(6, ErrorMessage = "Пароль должен содержать минимум 6 символов")]
        [Display(Name = "Пароль")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Подтвердите пароль")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        [Display(Name = "Подтверждение пароля")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Название компании")]
        public string? CompanyName { get; set; }

        [Display(Name = "Страна")]
        public string? Country { get; set; }
    }
}