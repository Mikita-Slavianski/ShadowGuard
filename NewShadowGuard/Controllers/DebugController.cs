using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewShadowGuard.Data;

namespace NewShadowGuard.Controllers
{
    public class DebugController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DebugController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Страница для генерации хеша и обновления БД
        public async Task<IActionResult> FixPasswords()
        {
            string password = "password123";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);

            // Обновляем всех пользователей
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                user.PasswordHash = hash;
            }
            await _context.SaveChangesAsync();

            // Проверяем, работает ли верификация
            bool verifyTest = BCrypt.Net.BCrypt.Verify(password, hash);

            ViewBag.Message = "✅ Пароли обновлены!";
            ViewBag.Hash = hash;
            ViewBag.VerifyTest = verifyTest ? "✅ Верификация работает" : "❌ Ошибка верификации";
            ViewBag.Users = users.Count;

            return View();
        }
    }
}