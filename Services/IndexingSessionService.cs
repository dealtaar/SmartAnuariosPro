using System.Data;
using System.IO;
using OfficeOpenXml; // Aprovecha el motor nativo de EPPlus que instalamos previamente

namespace SmartAnuariosPro.Services
{
    public class IndexingSessionService
    {
        /// <summary>
        /// Vuelca y guarda en caliente el estado actual de la grilla WPF (DataTable) en el archivo maestro Excel del disco duro.
        /// </summary>
        /// <param name="dataGridSource">Origen de datos con las filas de alumnos y columnas personalizadas.</param>
        /// <param name="localExcelPath">Ruta física completa donde se sobrescribirá la sesión.</param>
        public void SaveSessionHot(DataTable dataGridSource, string localExcelPath)
        {
            FileInfo file = new FileInfo(localExcelPath);

            // Si el archivo ya existía, lo removemos de forma segura para sobreescribir la sesión más fresca
            if (file.Exists)
                file.Delete();

            using (var package = new ExcelPackage())
            {
                // Agregamos la hoja de trabajo técnica asignada a la mesa de producción
                var ws = package.Workbook.Worksheets.Add("Indexacion_Sesion");

                // Carga masiva de alta velocidad desde la memoria RAM directa al formato de Excel
                ws.Cells["A1"].LoadFromDataTable(dataGridSource, true);

                // Aplicamos estilos visuales básicos e industriales para que el reporte sea legible
                ws.Row(1).Style.Font.Bold = true;

                // Ajuste automático del ancho de todas las columnas (incluyendo las inyectadas dinámicamente)
                if (ws.Dimension != null)
                {
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();
                }

                // Escribimos físicamente los bloques binarios finales en el disco
                package.SaveAs(file);
            }
        }
    }
}
