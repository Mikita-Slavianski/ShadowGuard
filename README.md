ShadowGuard
Веб-приложение для управления инцидентами кибербезопасности, разработанное на ASP.NET Core MVC. Предназначено для SOC-команд и позволяет обрабатывать инциденты, управлять активами и вести аудит действий.

✨ Возможности
•	🔐 Три роли пользователей: Администратор, Аналитик SOC, Клиент
•	🏢 Управление тенантами: компании с тарифными планами (Базовый/Профессиональный/Корпоративный)
•	🚨 Инциденты: создание, обработка, комментарии, эскалация, теги
•	💻 Активы: учёт серверов, рабочих станций, сетевого оборудования
•	📋 Журнал аудита: логирование всех действий пользователей
•	📤 Экспорт данных: выгрузка отчётов в Excel и CSV
•	🔄 Автоматическая корреляция: фоновая генерация инцидентов по правилам
•	🎨 Адаптивный интерфейс: на базе Bootstrap 5

🛠️ Технологии
Компонент	Технология
Платформа	ASP.NET Core 8 MVC
Язык	C# 12
ORM	Entity Framework Core
База данных	SQL Server (LocalDB)
Frontend	Bootstrap 5, Chart.js
Безопасность	BCrypt.Net
Экспорт	ClosedXML, CsvHelper

🚀 Быстрый старт

Требования
•	.NET 8 SDK
•	Visual Studio 2022 или VS Code
•	SQL Server LocalDB (входит в состав Visual Studio)

Установка
1.	Клонируйте репозиторий
git clone https://github.com/yourusername/cybersecurity-app.git
cd cybersecurity-app
2.	Создайте базу данных
o	Откройте Database/01_CreateDatabase.sql в SQL Server Management Studio или Visual Studio
o	Выполните скрипт для создания схемы и тестовых данных
3.	Настройте подключение
o	Откройте appsettings.json
o	Убедитесь, что имя базы данных совпадает с созданным:
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=NewShadowGuardDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
4.	Восстановите зависимости и запустите
dotnet restore
dotnet run
5.	Откройте в браузере
https://localhost:7XXX  (порт может отличаться)
