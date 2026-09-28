using System;
using System.IO;
using System.Threading.Tasks;
using OfficeOpenXml; // Incorpora el motor nativo de manipulación de hojas de cálculo

namespace SmartAnuariosPro.Services
{
    public class ExcelIntegrationService
    {
        public ExcelIntegrationService()
        {
            // CAMBIA ESTO (Línea 13 obsoleta):
            // ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // POR ESTO (Sintaxis oficial moderna de EPPlus 8+):
            ExcelPackage.License.SetNonCommercialPersonal("SmartAnuariosPro");
        }

        /// <summary>
        /// Fusiona el reporte web con el archivo local sin destruir columnas personalizadas previas de la mesa de indexación.
        /// </summary>
        /// <param name="localExcelPath">Ruta física de la matriz de indexación local (.xlsx).</param>
        /// <param name="webReportPath">Ruta física del reporte de elecciones bajado del hosting (.xlsx).</param>
        /// <param name="selectedPhotosFolder">Ruta destino física para el empaquetado de '\Fotos Seleccionadas\'.</param>
        public async Task MergeWebReportAsync(string localExcelPath, string webReportPath, string selectedPhotosFolder)
        {
            // Ejecución asíncrona dedicada en segundo plano para no congelar la pantalla del operario
            await Task.Run(() =>
            {
                FileInfo fileLocal = new FileInfo(localExcelPath);
                FileInfo fileWeb = new FileInfo(webReportPath);

                // Validamos la existencia física de las matrices antes de operar las celdas
                if (!fileLocal.Exists || !fileWeb.Exists) return;

                // Abrimos de forma nativa ambos flujos de datos en memoria RAM
                using (var packageLocal = new ExcelPackage(fileLocal))
                using (var packageWeb = new ExcelPackage(fileWeb))
                {
                    var sheetLocal = packageLocal.Workbook.Worksheets[0]; // Primera pestaña
                    var sheetWeb = packageWeb.Workbook.Worksheets[0];

                    int totalRowsLocal = sheetLocal.Dimension?.End.Row ?? 0;
                    int totalRowsWeb = sheetWeb.Dimension?.End.Row ?? 0;

                    // Mapeo exhaustivo celda por celda usando el N° de Orden (Columna 1) como llave única de cruce
                    for (int w = 2; w <= totalRowsWeb; w++)
                    {
                        var orderIdWeb = sheetWeb.Cells[w, 1].Text;
                        var selectedRostros = sheetWeb.Cells[w, 2].Text; // Formato de base de datos: "foto1.jpg, foto2.jpg"
                        var selectedFamiliar = sheetWeb.Cells[w, 3].Text;

                        for (int l = 2; l <= totalRowsLocal; l++)
                        {
                            var orderIdLocal = sheetLocal.Cells[l, 1].Text;

                            // Si encontramos la correspondencia exacta del alumno
                            if (orderIdWeb == orderIdLocal)
                            {
                                // Inyectamos las elecciones sin alterar ni tocar las columnas de páginas personalizadas que tenga a la derecha
                                sheetLocal.Cells[l, 3].Value = selectedRostros;
                                sheetLocal.Cells[l, 4].Value = selectedFamiliar;

                                // PASO 4 (Físico): Creamos de forma automatizada la subcarpeta numérica del alumno
                                string studentFolder = Path.Combine(selectedPhotosFolder, orderIdLocal);
                                if (!Directory.Exists(studentFolder))
                                    Directory.CreateDirectory(studentFolder);

                                // Generamos un archivo de manifiesto interno de control para auditar las elecciones del alumno
                                string manifestPath = Path.Combine(studentFolder, "manifiesto_elecciones.txt");
                                File.WriteAllText(manifestPath, $"Retrato Escogido: {selectedRostros}\nFamiliar Escogido: {selectedFamiliar}\nFecha de Sincronización: {DateTime.Now}");

                                break; // Pasamos al siguiente alumno del reporte web
                            }
                        }
                    }
                    // Guardamos y congelamos los cambios directamente en el disco duro local
                    packageLocal.Save();
                }
            });
        }
    }
}
