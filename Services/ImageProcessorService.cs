using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SmartAnuariosPro.Services
{
    public class ImageProcessorService
    {
        /// <summary>
        /// Procesa y clona las imágenes JPG de alta resolución reduciendo su tamaño de forma asíncrona y paralela.
        /// </summary>
        /// <param name="originPath">Ruta física origen con las fotos crudas/rostros aprobadas.</param>
        /// <param name="targetPath">Ruta física destino '\Fotos para Escoger\'.</param>
        /// <param name="pctWidth">Porcentaje de ancho (ej. 50).</param>
        /// <param name="pctHeight">Porcentaje de alto (ej. 50).</param>
        /// <param name="progressCallback">Acción delegada para reportar el avance en tiempo real (0 a 100) a la UI.</param>
        public async Task ResizeGalleryFolderAsync(
            string originPath,
            string targetPath,
            double pctWidth,
            double pctHeight,
            IProgress<int> progressCallback)
        {
            // Validaciones base del sistema de archivos
            if (!Directory.Exists(originPath)) return;
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            // Filtrado estricto de negativos fotográficos JPG locales
            string[] files = Directory.GetFiles(originPath, "*.jpg", SearchOption.TopDirectoryOnly);
            int totalFiles = files.Length;

            if (totalFiles == 0)
            {
                progressCallback?.Report(100);
                return;
            }

            // Conversión matemática de porcentajes a factores de escala (50% -> 0.5)
            double scaleX = pctWidth / 100.0;
            double scaleY = pctHeight / 100.0;
            int processedCount = 0;

            // Ejecución segura en un hilo secundario para mantener fluida la interfaz oscura
            await Task.Run(() =>
            {
                // Paralelización multinúcleo para aprovechar los procesadores del laboratorio fotográfico
                Parallel.ForEach(files, file =>
                {
                    try
                    {
                        string destinationFile = Path.Combine(targetPath, Path.GetFileName(file));

                        // Decodificación ultra veloz (Carga directa en memoria RAM liberando el archivo físico original)
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(file);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Congela el objeto para que múltiples hilos lo lean sin colisionar

                        // Redimensión por hardware / transformaciones nativas de WPF
                        TransformedBitmap scaledBitmap = new TransformedBitmap(bitmap, new ScaleTransform(scaleX, scaleY));

                        // Codificación JPEG de laboratorio (Calidad 75% = Peso súper pluma ideal para pixeleduca.com)
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.QualityLevel = 75;
                        encoder.Frames.Add(BitmapFrame.Create(scaledBitmap));

                        // Escritura física en disco duro mediante flujos seguros
                        using (FileStream stream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            encoder.Save(stream);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reescalando archivo {file}: {ex.Message}");
                    }
                    finally
                    {
                        // Incremento de hilos coordinado y seguro para actualizar la barra de progreso
                        int currentProcessed = Interlocked.Increment(ref processedCount);
                        progressCallback?.Report((int)((double)currentProcessed / totalFiles * 100));
                    }
                });
            });
        }
    }
}
