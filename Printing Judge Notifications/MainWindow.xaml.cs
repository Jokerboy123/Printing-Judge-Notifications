using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;

namespace Printing_Judge_Notifications
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<OwnerData> _rows = new();

        public MainWindow()
        {
            InitializeComponent();
            dataGrid.ItemsSource = _rows;
        }

        private async void UploadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xlsm|All Files|*.*",
                Title = "Выберите файл Excel"
            };

            if (dialog.ShowDialog() != true) return;

            UploadFileButton.IsEnabled = false;
            progressBar1.IsIndeterminate = false;
            progressBar1.Visibility = Visibility.Visible;

            try
            {
                var result = await Task.Run(() =>
                {
                    var list = new System.Collections.Generic.List<OwnerData>();

                    using var workbook = new XLWorkbook(dialog.FileName);
                    var worksheet = workbook.Worksheet(1);
                    var range = worksheet.RangeUsed();
                    if (range == null) return list;

                    // ToList() нужен, чтобы знать общее количество строк для прогресс-бара
                    var rows = range.RowsUsed().Skip(1).ToList();
                    int totalRows = rows.Count;
                    int processed = 0;

                    var culture = CultureInfo.GetCultureInfo("ru-RU");
                    foreach (var row in rows)
                    {
                        if (row.Cell(1).IsEmpty())
                        {
                            processed++;
                            continue;
                        }

                        // 1. ОДИН РАЗ на строку превращаем ячейки в список, чтобы можно было брать по индексу
                        var cellsList = row.Cells(1, 26).ToList();

                        try
                        {
                            var data = new OwnerData
                            {
                                // 2. Передаём ГОТОВЫЙ список (cellsList), а не row.Cells(...)
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

                            list.Add(data);
                        }
                        catch (Exception)
                        {
                            // Пропускаем проблемные строки
                        }

                        processed++;

                        if (processed % 200 == 0 || processed == totalRows)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar1.Maximum = totalRows;
                                progressBar1.Value = processed;
                            });
                        }
                    }

                    return list;
                });

                _rows.Clear();
                foreach (var item in result)
                {
                    _rows.Add(item);
                }
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

            if (cell.DataType == XLDataType.Text)
                return cell.GetString();

            return cell.Value.ToString();
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




        private void InitiationIP_PrintOut_Click(object sender, RoutedEventArgs e)
        {

        }

        private void InitiationIP_PDF_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelIP_PrintOut_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelIP_PDF_Click(object sender, RoutedEventArgs e)
        {

        }

      
    }
}
