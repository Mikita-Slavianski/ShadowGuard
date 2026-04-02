using System.ComponentModel.DataAnnotations;

namespace NewShadowGuard.Models.ViewModels
{
    public class PaymentViewModel
    {
        [Required(ErrorMessage = "Выберите тарифный план")]
        [Display(Name = "Тарифный план")]
        public string Plan { get; set; }

        [Required(ErrorMessage = "Выберите период продления")]
        [Range(1, 36, ErrorMessage = "Период должен быть от 1 до 36 месяцев")]
        [Display(Name = "Период")]
        public int Months { get; set; }

        [Required(ErrorMessage = "Введите номер карты")]
        [MinLength(13, ErrorMessage = "Номер карты должен содержать от 13 до 19 цифр")]
        [MaxLength(19, ErrorMessage = "Номер карты должен содержать от 13 до 19 цифр")]
        [RegularExpression(@"^[0-9\s]{13,19}$", ErrorMessage = "Номер карты должен содержать только цифры и пробелы")]
        [Display(Name = "Номер карты")]
        public string CardNumber { get; set; }

        [Required(ErrorMessage = "Введите имя владельца карты")]
        [MinLength(2, ErrorMessage = "Имя должно содержать минимум 2 символа")]
        [MaxLength(50, ErrorMessage = "Имя должно содержать максимум 50 символов")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Имя должно содержать только латинские буквы")]
        [Display(Name = "Владелец карты")]
        public string CardHolder { get; set; }

        [Required(ErrorMessage = "Введите срок действия карты")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Срок действия должен быть в формате ММ/ГГ (например, 12/25)")]
        [Display(Name = "Срок действия")]
        public string ExpiryDate { get; set; }

        [Required(ErrorMessage = "Введите CVV")]
        [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV должен содержать 3 или 4 цифры")]
        [Display(Name = "CVV")]
        public string Cvv { get; set; }
    }
}