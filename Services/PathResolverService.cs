using System;
using System.IO;

namespace SmartAnuariosPro.Services
{
    public class PathResolverService
    {
        // Ruta raíz en la PC según la regla industrial v2026
        public string BaseMasterPath { get; set; } = @"D:\Anuarios2026\Fotógrafo";

        /// <summary>
        /// Purga espacios y fuerza la primera letra en mayúscula (ej: "Francis_6to")
        /// </summary>
        public static string FormatearCodigoColegio(string codigoSucio)
        {
            if (string.IsNullOrWhiteSpace(codigoSucio)) return string.Empty;

            string limpio = codigoSucio.Replace(" ", "").Trim();

            if (limpio.Length > 0)
            {
                // Corrección segura de tipos: forzamos el carácter a string explícito
                limpio = char.ToUpper(limpio[0]).ToString() + limpio.Substring(1);
            }
            return limpio;
        }

        /// <summary>
        /// Resuelve la jerarquía física auditada real: [Espacio de Trabajo] \ [Nombre Fotógrafo] \ [Código Colegio]
        /// </summary>
        public string GetClassroomFolder(string photographer, string school)
        {
            string codigoLimpio = FormatearCodigoColegio(school);
            string fotografoLimpio = photographer.Trim();

            // 🛡️ COMBINACIÓN TRIPLE REAL EN EL BUS DE DATOS
            return Path.Combine(BaseMasterPath, fotografoLimpio, codigoLimpio);
        }



        /// <summary>
        /// Devuelve la ruta de una subcarpeta específica y la crea en el disco si no existe (3 argumentos nativos).
        /// </summary>
        public string GetSubFolder(string photographer, string school, string subFolder)
        {
            // Mapeamos los alias antiguos a la nueva nomenclatura estricta en Mayúsculas del pipeline
            string subFolderCorregida = subFolder;
            if (subFolder == "Fotos Rostro") subFolderCorregida = "Individuales";
            else if (subFolder == "Fotos Familiar") subFolderCorregida = "Familiares";
            else if (subFolder == "Fotos Evento") subFolderCorregida = "Evento";
            else if (subFolder == "Fotos Seleccionadas") subFolderCorregida = "Escogidas";
            else if (subFolder == "Fotos para Escoger") subFolderCorregida = "Fotos Por Escoger";

            string root = GetClassroomFolder(photographer, school);
            string finalPath = Path.Combine(root, subFolderCorregida);

            if (!Directory.Exists(finalPath))
                Directory.CreateDirectory(finalPath);

            return finalPath;
        }

        /// <summary>
        /// Sobrecarga de 2 argumentos para no romper la lógica de las fases en MainWindow
        /// </summary>
        public string GetSubFolder(string school, string subFolder)
        {
            return GetSubFolder(string.Empty, school, subFolder);
        }

        /// <summary>
        /// Inicializa físicamente las carpetas en la PC con iniciales en Mayúscula.
        /// </summary>
        public void InitializeAIFolders(string photographer, string school)
        {
            if (string.IsNullOrWhiteSpace(BaseMasterPath))
                throw new InvalidOperationException("La ruta de la carpeta maestra no ha sido definida.");

            // Árbol con iniciales en Mayúsculas exigido por las reglas consolidadas
            string[] coreFolders = {
                "Individuales",
                "Familiares",
                "Evento",
                "Escogidas",
                "Fotos Por Escoger",
                "Fotos Sobrantes"
            };

            foreach (var folder in coreFolders)
            {
                GetSubFolder(photographer, school, folder);
            }
        }
    }
}
