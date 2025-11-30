# FlexAutomator

**FlexAutomator** — це інструмент для автоматизації рутинних дій користувача на ОС Windows. Дозволяє створювати візуальні сценарії (Правила), які складаються з тригерів та послідовності дій.

## 🚀 Функціонал

*   **Візуальний редактор:** Створення сценаріїв без написання коду.
*   **Тригери:**
    *   ⏰ Час (розклад).
    *   ⌨️ Гарячі клавіші (Global Hotkeys).
    *   📂 Зміна файлів/папок.
    *   🤖 Команди Telegram.
*   **Дії:**
    *   Емуляція миші та клавіатури.
    *   Керування процесами (запуск/зупинка).
    *   Інтеграція з API (YouTube, TMDb).
    *   Сповіщення в Telegram.
*   **Архітектура:** WPF, MVVM, Dependency Injection, SQLite, Entity Framework Core.

## 🛠 Технології

*   .NET 8
*   WPF (Windows Presentation Foundation)
*   Entity Framework Core (SQLite)
*   Material Design In XAML
*   Telegram.Bot

## 📥 Як запустити (для користувача)

1.  Зайдіть у розділ **[Releases](../../releases)** цього репозиторію.
2.  Завантажте архів `FlexAutomator_Build.zip`.
3.  Розпакуйте архів у зручне місце.
4.  Запустіть файл `FlexAutomator.exe`.
5.  При першому запуску програма попросить вказати шлях для створення бази даних.

## 💻 Як запустити (для розробника)

1.  Клонуйте репозиторій:
    ```bash
    git clone https://github.com/SpanchBob22/FlexAutomator.git
    ```
2.  Відкрийте файл `FlexAutomator.sln` у Visual Studio 2022.
3.  Переконайтеся, що встановлено .NET 8 SDK.
4.  Натисніть `F5` для запуску.
