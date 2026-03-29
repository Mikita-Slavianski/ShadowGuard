using System.ComponentModel.DataAnnotations;

namespace NewShadowGuard.Models.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Введите текущий пароль")]
        [Display(Name = "Текущий пароль")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "Введите новый пароль")]
        [MinLength(6, ErrorMessage = "Пароль должен содержать минимум 6 символов")]
        [Display(Name = "Новый пароль")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Подтвердите новый пароль")]
        [Compare("NewPassword", ErrorMessage = "Пароли не совпадают")]
        [Display(Name = "Подтверждение нового пароля")]
        public string ConfirmPassword { get; set; }
    }
}