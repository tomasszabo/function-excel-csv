using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Company.Function
{
	public static class Excel2CSV
	{
		[FunctionName("excel2csv")]
		public static async Task<IActionResult> Run(
				[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
				ILogger log)
		{
			log.LogInformation("Converting excel to CSV");

      var buffer = new byte[req.Body.Length];
      await req.Body.ReadAsync(buffer);

			log.LogInformation("Reading input");

			MemoryStream stream = new MemoryStream();
			stream.Write(buffer, 0, buffer.Length);
			stream.Position = 0;

			var result = new List<CSVResult>();

			log.LogInformation("Opening excel file");

      using var document = SpreadsheetDocument.Open(stream, false);
      var sheets = document.WorkbookPart.Workbook.Descendants<Sheet>();

			log.LogInformation("Walking through {0} sheets", sheets.Count());

			foreach (Sheet sheet in sheets)
			{
				log.LogInformation("Processing sheet {0}", sheet.Name);

				WorksheetPart worksheetPart = (WorksheetPart) document.WorkbookPart.GetPartById(sheet.Id);
				Worksheet worksheet = worksheetPart.Worksheet;

				SharedStringTablePart stringTable = document.WorkbookPart.GetPartsOfType<SharedStringTablePart>().First();
				SharedStringItem[] stringItem = stringTable.SharedStringTable.Elements<SharedStringItem>().ToArray();

				string csvName = sheet.Name;
				var csvContent = new StringBuilder();

				log.LogInformation("Converting to CSV");

        foreach (var row in worksheet.Descendants<Row>())
        {
          var stringBuilder = new StringBuilder();
          foreach (Cell cell in row)
          {
            string value = string.Empty;
            if (cell.CellValue != null)
            {
              if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString) {
                value = stringItem[int.Parse(cell.CellValue.Text)].InnerText;
              } else {
                value = cell.CellValue.Text;
              }
            }

            stringBuilder.Append(string.Format("{0},", value.Trim()));
          }

          csvContent.Append(stringBuilder.ToString().TrimEnd(','));
        }

				log.LogInformation("Adding to result");

        result.Add(new CSVResult { 
					Name = string.Format("{0}.csv", csvName),
					Content = csvContent.ToString() 
				});
        
      }

			log.LogInformation("Conversion done, returning CSV result");

			return new JsonResult(result);
		}
	}

	public class CSVResult
	{
		public string Name { get; set; }
		public string Content { get; set; }
	}
}
