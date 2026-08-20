using ClosedXML;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Printing_Judge_Notifications
{
    
    public partial class MainWindow : Window
    {
        private ObservableCollection<OwnerData> _rows = new();


        public MainWindow()
        {
            InitializeComponent();

            dataGrid.ItemsSource = _rows;
        }
        private void UpdateProgress(int percent)
        {
            int safePercent = Math.Min(100, Math.Max(0, percent));

            if (Application.Current.Dispatcher.CheckAccess())
            {
                progressBar1.Value = safePercent;
                // Если у тебя есть TextBlock/Label для процентов — обновляй его здесь
                // progressLabel.Text = $"{safePercent}%";
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    progressBar1.Value = safePercent;
                    // progressLabel.Text = $"{safePercent}%";
                });
            }
        }

        private async void UploadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xlsm|All Files|*.*",
                Title = "Выберите файл Excel"
            };

            if (!dialog.ShowDialog().GetValueOrDefault())
                return;

            UploadFileButton.IsEnabled = false;
            progressBar1.IsIndeterminate = false;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            progressBar1.Visibility = Visibility.Visible;

            try
            {
                _rows.Clear();

                // ЭТАП 1: подготовка (5%)
                UpdateProgress(5);

                var result = await Task.Run(() =>
                {
                    var tempList = new List<OwnerData>();

                    using var workbook = new XLWorkbook(dialog.FileName);
                    var worksheet = workbook.Worksheet(1);
                    var range = worksheet.RangeUsed();
                    if (range == null)
                        return tempList;

                    var rows = range.RowsUsed().Skip(1).ToList();
                    int totalRows = rows.Count;
                    int processed = 0;

                    if (totalRows == 0)
                        return tempList;

                    var culture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");

                    foreach (var row in rows)
                    {
                        if (row.Cell(1).IsEmpty())
                        {
                            processed++;
                            continue;
                        }

                        var cellsList = row.Cells(1, 26).ToList();

                        try
                        {
                            var data = new OwnerData
                            {
                                CourtOfficer = GetStringFast(cellsList, 0),
                                DateOfVerification = GetDateFast(cellsList, 1, culture),
                                NumberOfJudicalArea = GetStringFast(cellsList, 2),
                                Account = GetStringFast(cellsList, 3),
                                FullName = GetStringFast(cellsList, 4),
                                Part = GetStringFast(cellsList, 5),
                                Town = GetStringFast(cellsList, 6),
                                Street = GetStringFast(cellsList, 7),
                                Building = GetStringFast(cellsList, 8),
                                Korps = GetStringFast(cellsList, 9),
                                Flat = GetStringFast(cellsList, 10),
                                Room = GetStringFast(cellsList, 11),
                                RequestMCDate = GetStringFast(cellsList, 12),
                                OrderNumber = GetStringFast(cellsList, 13),
                                OrderDate = GetDateFast(cellsList, 14, culture),
                                TransferDateFSSP = GetDateFast(cellsList, 15, culture),
                                InitiationDate = GetDateFast(cellsList, 16, culture),
                                Appendex = GetStringFast(cellsList, 17),
                                BirthDate = GetDateFast(cellsList, 18, culture),
                                Period = GetStringFast(cellsList, 19),

                                Debt = GetDecimalFast(cellsList, 20, culture),
                                Punishment = GetDecimalFast(cellsList, 21, culture),
                                Duty = GetDecimalFast(cellsList, 22, culture),
                                Valuation = GetDecimalFast(cellsList, 23, culture),
                                AmountSum = GetDecimalFast(cellsList, 24, culture),

                                Document = GetStringFast(cellsList, 25)
                            };

                            tempList.Add(data);
                        }
                        catch (Exception)
                        {
                            // Пропускаем проблемные строки
                        }

                        processed++;

                        // Обновляем прогресс каждые 50 строк ИЛИ в конце
                        if (processed % 50 == 0 || processed == totalRows)
                        {
                            int percent = (processed * 80) / totalRows; // 80% — это максимум на этапе чтения
                            percent = Math.Min(80, percent);           // не больше 80

                            Application.Current.Dispatcher.Invoke(() => UpdateProgress(percent));
                        }
                    }

                    return tempList;
                });

                // ЭТАП 2: копирование в ObservableCollection (10%)
                foreach (var item in result)
                {
                    _rows.Add(item);
                }
                UpdateProgress(90);

                // ЭТАП 3: финальная привязка и завершение (10%)
                dataGrid.ItemsSource = _rows;
                UpdateProgress(100);
            }
            catch (System.IO.IOException)
            {
                MessageBox.Show("Файл занят. Закройте его в Excel и попробуйте снова.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка: {ex.Message}");
            }
            finally
            {
                UploadFileButton.IsEnabled = true;
                progressBar1.Visibility = Visibility.Collapsed;
            }
        }


        // Было: IXLCells cells
        // Стало: IList<IXLCell> cells
        private string GetStringFast(IList<IXLCell> cells, int index)
        {
            if (index < 0 || index >= cells.Count) return null;

            var cell = cells[index]; // Теперь это работает, потому что cells — это List
            if (cell.IsEmpty()) return null;

            //if (cell.DataType == XLDataType.Text)
            //    return cell.GetString();

            return cell.GetString();
        }

        private DateTime? GetDateFast(IList<IXLCell> cells, int index, CultureInfo culture)
        {
            if (index < 0 || index >= cells.Count) return null;

            var cell = cells[index];
            if (cell.IsEmpty()) return null;

            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime();

            if (cell.DataType == XLDataType.Number)
            {
                double d = cell.GetDouble();
                if (d >= 1 && d < 300000)
                {
                    try
                    {
                        return DateTime.FromOADate(d);
                    }
                    catch { }
                }
            }

            var str = cell.GetString()?.Trim();
            if (!string.IsNullOrEmpty(str) &&
                DateTime.TryParse(str, culture, DateTimeStyles.None, out var parsed))
                return parsed;

            return null;
        }
        private decimal? GetDecimalFast(IList<IXLCell> cells, int index, CultureInfo culture)
        {
            if (index < 0 || index >= cells.Count) return null;

            var cell = cells[index];
            if (cell.IsEmpty()) return null;

            if (cell.DataType == XLDataType.Number)
                return (decimal)cell.GetDouble();

            var raw = cell.GetString()?.Trim()
                ?.Replace("руб", "", StringComparison.OrdinalIgnoreCase)
                ?.Replace("₽", "")
                ?.Replace("\u00A0", "")
                ?.Replace(" ", "");

            if (!string.IsNullOrEmpty(raw) &&
                decimal.TryParse(raw, NumberStyles.Any, culture, out var result))
                return result;

            return null;
        }

        private void InitiationIP_PDF_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dataGrid.SelectedItems.Cast<OwnerData>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите хотя бы одну строку в таблице!");
                return;
            }

            var data = selectedItems.First();
            //   string baseDir = Environment.UserName + @"C:\Users\Ларина\source\repos\Printing-Judge-Notifications\Printing Judge Notifications";
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TEMPLATES");

            string templatePath = "";
            // если в строке есть "за", то выбирать первый шаблон, иначе выбирать второй шаблон
            if (data.FullName.Contains(" за "))
            {
                templatePath = Path.Combine(baseDir, "TEMPLATE_INITIATION_CHILD.docx");
            }
            else
            {
                templatePath = Path.Combine(baseDir, "TEMPLATE_INITIATION_ADULT.docx");
            }
            string outputDir = Path.Combine(baseDir, "Output");
            Directory.CreateDirectory(outputDir);
            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Шаблон не найден в папке {templatePath}");
                return; 
            }
            try
            {
                var printer = new DocumentPrinter();
                string safeName = Regex.Replace(data.FullName ?? "unknown", @"[\\/:*?""<>|]", "_");
                string fileName = $"Заявление о возбуждении ИП_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                string outputPath = Path.Combine(outputDir, fileName);
                printer.GenerateTemplate(templatePath, outputPath, data);

                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true,
                    Verb = "Open",

                });

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при создании документа:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }
        private void InitiationIP_PrintOut_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dataGrid.SelectedItems.Cast<OwnerData>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите хотя бы одну строку в таблице!");
                return;
            }

            var data = selectedItems.First();
            string baseDir = @"C:\Users\Ларина\source\repos\Printing Judge Notifications\Printing Judge Notifications";
            // если в строке есть "за", то выбирать первый шаблон, иначе выбирать второй шаблон

            string templatePath = Path.Combine(baseDir, "TEMPLATE_INITIATION_ADULT.docx");
            string outputDir = Path.Combine(baseDir, "Output");
            Directory.CreateDirectory(outputDir);
            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Шаблон не найден в папке {templatePath}");
                return;
            }
            try
            {
                var printer = new DocumentPrinter();
                string safeName = Regex.Replace(data.FullName ?? "unknown", @"[\\/:*?""<>|]", "_");
                string fileName = $"Заявление о возбуждении ИП_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                string outputPath = Path.Combine(outputDir, fileName);
                printer.GenerateTemplate(templatePath, outputPath, data);

                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true,
                    Verb = "Print",

                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при создании документа:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }
        private void CancelIP_PDF_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dataGrid.SelectedItems.Cast<OwnerData>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите хотя бы одну строку в таблице!");
                return;
            }

            // пройтись по массиву selectedItems и провести эту манипуляцию для каждого элемента массива
            var data = selectedItems.First();

            string baseDir = (@"C:\Users\Ларина\source\repos\Printing Judge Notifications\Printing Judge Notifications");
            string templatePath = Path.Combine(baseDir, "TEMPLATE_CANCEL_ADULT.docx");
            string outputDir = Path.Combine(baseDir, "Output");

            Directory.CreateDirectory(outputDir);

            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Шаблон не найден в папке {templatePath}");
                return;
            }
            try
            {
                var printer = new DocumentPrinter();

                // 3. Формируем безопасное имя файла (убираем запрещенные символы из ФИО)
                string safeName = Regex.Replace(data.FullName ?? "unknown", @"[\\/:*?""<>|]", "_");
                string fileName = $"Заявление_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                string outputPath = Path.Combine(outputDir, fileName);

                printer.GenerateTemplate(templatePath, outputPath, data);

                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true,
                    Verb = "Open",

                });


            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при создании документа:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }
        private void CancelIP_PDF_PRINTOUT_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dataGrid.SelectedItems.Cast<OwnerData>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите хотя бы одну строку в таблице!");
                return;
            }

            // пройтись по массиву selectedItems и провести эту манипуляцию для каждого элемента массива
            var data = selectedItems.First();
          
            string baseDir = @"C:\Users\Ларина\source\repos\Printing Judge Notifications\Printing Judge Notifications";
            // если в строке есть "за", то выбирать первый шаблон, иначе выбирать второй шаблон

            string templatePath = Path.Combine(baseDir, "TEMPLATE_CANCEL_ADULT.docx");

            string outputDir = Path.Combine(baseDir, "Output");

            Directory.CreateDirectory(outputDir);

            if (!File.Exists(templatePath))
            {
                MessageBox.Show($"Шаблон не найден в папке {templatePath}");
                return;
            }
            try
            {
                var printer = new DocumentPrinter();

                // 3. Формируем безопасное имя файла (убираем запрещенные символы из ФИО)
                string safeName = Regex.Replace(data.FullName ?? "unknown", @"[\\/:*?""<>|]", "_");
                string fileName = $"Заявление о прекращении ИП_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
                string outputPath = Path.Combine(outputDir, fileName);

                printer.GenerateTemplate(templatePath, outputPath, data);

                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true,
                    Verb = "Print",
                    
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при создании документа:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }

       
        //private void Button_Click_1(object sender, RoutedEventArgs e)
        //{
        //    string fullName = dataGrid.SelectedItem.GetMethod ToString();
        //    MessageBox.Show(fullName);
        //}
    }
}
