using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace NewShadowGuard.Services
{
    public class ExportService
    {
        // Экспорт в Excel
        public byte[] ExportToExcel<T>(List<T> data, string sheetName = "Data")
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add(sheetName);

                // Заголовки
                var properties = typeof(T).GetProperties()
                    .Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType == typeof(DateTime));

                int col = 1;
                foreach (var prop in properties)
                {
                    worksheet.Cell(1, col).Value = prop.Name;
                    worksheet.Cell(1, col).Style.Font.Bold = true;
                    worksheet.Cell(1, col).Style.Fill.BackgroundColor = XLColor.LightBlue;
                    col++;
                }

                // Данные
                int row = 2;
                foreach (var item in data)
                {
                    col = 1;
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(item);
                        worksheet.Cell(row, col).Value = value?.ToString() ?? "";
                        col++;
                    }
                    row++;
                }

                // Авто-ширина колонок
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        // Экспорт в CSV
        public byte[] ExportToCsv<T>(List<T> data)
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csv.WriteRecords(data);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}