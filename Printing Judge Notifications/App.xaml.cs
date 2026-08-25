using System;
using System.Windows;
using System.Windows.Threading;

namespace Printing_Judge_Notifications
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Подписываемся на событие: если где-то в UI (окнах, кнопках) случится ошибка,
            // она попадет сюда, а не убьет приложение молча
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Формируем понятное сообщение об ошибке
            string errorMessage = $"Произошла критическая ошибка:\n\n{e.Exception.Message}\n\n" +
                                  $"Детали (StackTrace):\n{e.Exception.StackTrace}";

            // Показываем пользователю (и тебе как разработчику) реальную причину
            MessageBox.Show(errorMessage, "Ошибка приложения", MessageBoxButton.OK, MessageBoxImage.Error);

            // Важно: говорим WPF, что ошибку мы "обработали" (хотя и просто показали её).
            // Без этой строки приложение всё равно закроется, но уже после показа окна.
            e.Handled = true;
        }
    }
}
