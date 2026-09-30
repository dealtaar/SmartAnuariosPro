using MySql.Data.MySqlClient; // Incorpora el driver oficial industrial de conexión a MySQL
using System;
using System.Data;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartAnuariosPro.Services
{
    public class DatabaseService
    {
        // Cadena de conexión técnica apuntando al hosting remoto pixeleduca.com
        // Cadena de conexión industrial externa calibrada con el puerto nativo de StackCP
        private readonly string _connectionString = "Server=mysql.us.stackcp.com;Port=44529;Database=smartanuarios_db-353039387e2e;Uid=smartanuarios_db-353039387e2e;Pwd=smartanuarios2026;";

        /// <summary>
        /// Cambia el estado del candado web en la base de datos remota para bloquear o permitir el acceso de los padres (Punto 3).
        /// </summary>
        /// <param name="schoolId">Identificador único del colegio en la base de datos.</param>
        /// <param name="isLocked">True para bloquear el acceso web, False para aperturarlo.</param>
        public async Task<bool> UpdateWebLockStatusAsync(int id, bool estado)
        {
            try
            {
                // Convertimos el booleano al entero que entiende tu base de datos (1 para liberado, 0 para bloqueado)
                int valorNumerico = estado ? 1 : 0;

                using (var conexion = new MySqlConnection(_connectionString))
                {
                    await conexion.OpenAsync();

                    // 🟢 CORREGIDO: Cambiamos 'web_bloqueado' por tu columna real de MariaDB 'acceso_liberado'
                    string consultaSql = "UPDATE alumnos SET acceso_liberado = @estado WHERE id = @id";

                    using (var comando = new MySqlCommand(consultaSql, conexion))
                    {
                        comando.Parameters.AddWithValue("@estado", valorNumerico);
                        comando.Parameters.AddWithValue("@id", id);

                        int filasAfectadas = await comando.ExecuteNonQueryAsync();
                        return filasAfectadas > 0;
                    }
                }
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error de conexión MySQL: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Corrección administrativa blindada contra inyección SQL de los textos digitados por un operador.
        /// </summary>
        public async Task<bool> UpdateStudentTextAsync(int studentId, string fieldName, string newText)
        {
            // Diccionario estricto de columnas autorizadas en producción (White-list)
            var columnasValidas = new System.Collections.Generic.Dictionary<string, string>
            {
                { "nombre", "nombre_completo" },
                { "apellido", "apellido" },
                { "frase", "frase" }
            };

            // Validación rigurosa basada en el diccionario seguro
            if (string.IsNullOrWhiteSpace(fieldName) || !columnasValidas.ContainsKey(fieldName.ToLower().Trim()))
            {
                System.Diagnostics.Debug.WriteLine($"[SEGURIDAD BLOQUEADA] Intento de acceso a columna no autorizada: {fieldName}");
                return false;
            }

            string columnaRealSegura = columnasValidas[fieldName.ToLower().Trim()];
            string query = $"UPDATE alumnos SET {columnaRealSegura} = @text WHERE id = @id";

            using (var conn = new MySqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@text", newText ?? "");
                        cmd.Parameters.AddWithValue("@id", studentId);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error de escritura segura MySQL: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Actualiza la matriz completa de datos, códigos de imágenes y demografía de un alumno en la nube.
        /// </summary>
        public async Task<bool> UpdateStudentFullDataAsync(AlumnoRemoteModel al)
        {
            string query = @"UPDATE alumnos SET 
                        nombre_completo = @nombre,
                        fecha_nacimiento = @fecha,
                        sexo = @sexo,
                        foto_rostro = @rostro,
                        foto_familiar = @familiar,
                        preferencias = @preferencias,
                        comida_favorita = @comida,
                        profesion = @profesion 
                     WHERE id = @id";

            using (var conn = new MySqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@nombre", al.NombreCompleto ?? "");
                        cmd.Parameters.AddWithValue("@fecha", al.FechaNacimiento.HasValue ? (object)al.FechaNacimiento.Value : DBNull.Value); // 🟢 Corrección exacta
                        cmd.Parameters.AddWithValue("@sexo", al.Sexo ?? "");
                        cmd.Parameters.AddWithValue("@rostro", al.FotoRostro ?? "");
                        cmd.Parameters.AddWithValue("@familiar", al.FotoFamiliar ?? "");
                        cmd.Parameters.AddWithValue("@preferencias", al.Hobbies ?? "");
                        cmd.Parameters.AddWithValue("@comida", al.ComidaFav ?? "");
                        cmd.Parameters.AddWithValue("@profesion", al.Profesion ?? "");
                        cmd.Parameters.AddWithValue("@id", al.Id);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al actualizar datos en tabla real: {ex.Message}");
                    return false;
                }
            }
        }

        // MÉTODOS ASÍNCRONOS PARA TU CLASE DATABASE SERVICE:
        /// <summary>
        /// Registra de forma transaccional un colegio y su sección inicial obligatoria en el hosting.
        /// </summary>
        public async Task<int> InsertarColegioEstructuraAsync(string codigoColegio, string nombreColegio, string fotografo)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        int colegioId = 0;
                        string queryColegio = @"INSERT INTO colegios (id_estudio, nombre_colegio, codigo_colegio, fotografo, fecha_registro) 
                                        VALUES (1, @nombre, @codigo, @fotografo, NOW());
                                        SELECT LAST_INSERT_ID();";

                        using (var cmd = new MySqlCommand(queryColegio, conn, (MySqlTransaction)trans))
                        {
                            cmd.Parameters.AddWithValue("@nombre", nombreColegio);
                            cmd.Parameters.AddWithValue("@codigo", codigoColegio);
                            cmd.Parameters.AddWithValue("@fotografo", fotografo);

                            colegioId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        int seccionId = 0;
                        string querySeccion = @"INSERT INTO secciones (colegio_id, nombre_seccion) VALUES (@colegioId, 'Seccion Unica');
                                        SELECT LAST_INSERT_ID();";

                        using (var cmd = new MySqlCommand(querySeccion, conn, (MySqlTransaction)trans))
                        {
                            cmd.Parameters.AddWithValue("@colegioId", colegioId);
                            seccionId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        await trans.CommitAsync();
                        return seccionId; // Retornamos el ID de la sección para inyectar los alumnos inmediatamente
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        System.Diagnostics.Debug.WriteLine($"Fallo transaccional MariaDB: {ex.Message}");
                        return -1;
                    }
                }
            }
        }

        /// <summary>
        /// Carga masiva ultrarrápida del lote de alumnos del aula mediante comandos parametrizados secuenciales.
        /// </summary>
        public async Task<bool> InsertarLoteAlumnosAsync(System.Collections.Generic.List<AlumnoRemoteModel> alumnos)
        {
            if (alumnos.Count == 0) return true;

            // ✔️ SQL CORREGIDO: Apunta a la tabla 'alumnos' y usa 'nombre_completo' de producción
            string query = @"INSERT INTO alumnos (seccion_id, codigo_colegio, numero_orden, nombre_completo, completado, acceso_liberado, foto_rostro, foto_familiar) 
                     VALUES (@seccion, @codigo, @orden, @nombre, 'NO', 1, @rostro, @familiar);";

            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        foreach (var al in alumnos)
                        {
                            using (var cmd = new MySqlCommand(query, conn, (MySqlTransaction)trans))
                            {
                                cmd.Parameters.AddWithValue("@seccion", al.SeccionId);
                                cmd.Parameters.AddWithValue("@codigo", al.CodigoColegio);
                                cmd.Parameters.AddWithValue("@orden", al.NumeroOrden);
                                cmd.Parameters.AddWithValue("@nombre", al.NombreCompleto);

                                // Parámetros cortos de fotos iniciales (vacíos si no se adjuntaron)
                                cmd.Parameters.AddWithValue("@rostro", al.FotoRostro ?? "");
                                cmd.Parameters.AddWithValue("@familiar", al.FotoFamiliar ?? "");

                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        await trans.CommitAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        System.Diagnostics.Debug.WriteLine($"Fallo al insertar lote de alumnos: {ex.Message}");
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Recupera de forma asíncrona la lista de colegios activos para los monitores de la app.
        /// </summary>
        public async Task<System.Collections.Generic.List<ColegioRemoteModel>> ObtenerReporteColegiosAsync()
        {
            var lista = new System.Collections.Generic.List<ColegioRemoteModel>();
            string query = "SELECT id, codigo_colegio, nombre_colegio, fotografo FROM colegios ORDER BY id DESC";

            using (var conn = new MySqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new ColegioRemoteModel
                            {
                                Id = reader.GetInt32("id"),
                                CodigoColegio = reader.GetString("codigo_colegio"),
                                NombreColegio = reader.GetString("nombre_colegio"),
                                Fotografo = reader.IsDBNull(reader.GetOrdinal("fotografo")) ? "" : reader.GetString("fotografo")
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error MySQL ObtenerReporte: {ex.Message}");
                }
            }
            return lista;
        }

        /// <summary>
        /// Descarga en vivo desde el hosting la lista de alumnos de un colegio usando su código alfanumérico.
        /// </summary>
        public async Task<System.Collections.Generic.List<AlumnoRemoteModel>> ObtenerAlumnosPorColegioAsync(string codigoColegio)
        {
            var lista = new System.Collections.Generic.List<AlumnoRemoteModel>();

            // 🟢 SQL EVOLUCIONADO: Unificamos tablas para traer el fotógrafo asignado al colegio de cada alumno
            string query = @"SELECT a.id, a.seccion_id, a.codigo_colegio, a.numero_orden, a.nombre_completo, a.completado, 
                            a.acceso_liberado, a.foto_rostro, a.foto_familiar, a.preferencias, a.comida_favorita, 
                            a.profesion, a.fecha_nacimiento, a.sexo, c.fotografo
                     FROM alumnos a
                     INNER JOIN colegios c ON a.codigo_colegio = c.codigo_colegio
                     WHERE a.codigo_colegio = @codigo ORDER BY a.numero_orden ASC";

            using (var conn = new MySqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@codigo", codigoColegio);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var alumno = new AlumnoRemoteModel
                                {
                                    Id = reader.GetInt32("id"),
                                    SeccionId = reader.GetInt32("seccion_id"),
                                    CodigoColegio = reader.GetString("codigo_colegio"),
                                    NumeroOrden = reader.GetInt32("numero_orden"),
                                    NombreCompleto = reader.GetString("nombre_completo"),

                                    // 🟢 CAPTURA EN FILA: Inyectamos el fotógrafo real del registro indexado
                                    Fotografo = reader.IsDBNull(reader.GetOrdinal("fotografo")) ? "Fotógrafo" : reader.GetString("fotografo"),

                                    Hobbies = reader.IsDBNull(reader.GetOrdinal("preferencias")) ? string.Empty : reader.GetString("preferencias"),
                                    ComidaFav = reader.IsDBNull(reader.GetOrdinal("comida_favorita")) ? string.Empty : reader.GetString("comida_favorita"),
                                    Profesion = reader.IsDBNull(reader.GetOrdinal("profesion")) ? string.Empty : reader.GetString("profesion"),

                                    FotoRostro = reader.IsDBNull(reader.GetOrdinal("foto_rostro")) || string.IsNullOrWhiteSpace(reader.GetString("foto_rostro"))
                                                 ? "-- Ninguna --" : reader.GetString("foto_rostro"),

                                    FotoFamiliar = reader.IsDBNull(reader.GetOrdinal("foto_familiar")) || string.IsNullOrWhiteSpace(reader.GetString("foto_familiar"))
                                                   ? "-- Ninguna --" : reader.GetString("foto_familiar"),

                                    AccesoLiberado = reader.GetInt32("acceso_liberado"),
                                    FechaNacimiento = reader.IsDBNull(reader.GetOrdinal("fecha_nacimiento")) ? null : (DateTime?)reader.GetDateTime("fecha_nacimiento"),
                                    Sexo = reader.IsDBNull(reader.GetOrdinal("sexo")) || string.IsNullOrWhiteSpace(reader.GetString("sexo"))
                                           ? "-- Elige --" : reader.GetString("sexo"),
                                };

                                lista.Add(alumno);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error MySQL ObtenerAlumnosPorColegio: {ex.Message}");
                }
            }
            return lista;
        }

        /// <summary>
        /// Actualiza los campos extendidos de un alumno desde la grilla de edición.
        /// </summary>
        public async Task<bool> UpdateStudentExtendedDataAsync(int studentId, string hobbies, string comidaFav, string profesion)
        {
            string query = "UPDATE alumnos SET preferencias = @hobbies, comida_favorita = @comida, profesion = @profesion WHERE id = @id";

            using (var conn = new MySqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@hobbies", hobbies ?? "");
                        cmd.Parameters.AddWithValue("@comida", comidaFav ?? "");
                        cmd.Parameters.AddWithValue("@profesion", profesion ?? "");
                        cmd.Parameters.AddWithValue("@id", studentId);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al actualizar datos extendidos: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Elimina un colegio y todos sus alumnos asociados en cascada de forma transaccional.
        /// </summary>
        public async Task<bool> EliminarColegioCompletoAsync(int schoolId)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Eliminar alumnos asociados a las secciones del colegio
                        string deleteAlumnos = "DELETE FROM alumnos WHERE seccion_id IN (SELECT id FROM secciones WHERE colegio_id = @id)";
                        using (var cmd = new MySqlCommand(deleteAlumnos, conn, (MySqlTransaction)trans))
                        {
                            cmd.Parameters.AddWithValue("@id", schoolId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Eliminar las secciones del colegio
                        string deleteSecciones = "DELETE FROM secciones WHERE colegio_id = @id";
                        using (var cmd = new MySqlCommand(deleteSecciones, conn, (MySqlTransaction)trans))
                        {
                            cmd.Parameters.AddWithValue("@id", schoolId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 3. Eliminar el colegio
                        string deleteColegio = "DELETE FROM colegios WHERE id = @id";
                        using (var cmd = new MySqlCommand(deleteColegio, conn, (MySqlTransaction)trans))
                        {
                            cmd.Parameters.AddWithValue("@id", schoolId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await trans.CommitAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        System.Diagnostics.Debug.WriteLine($"Error en eliminación en cascada: {ex.Message}");
                        return false;
                    }
                }
            }
        }
    }

    public class ColegioRemoteModel
    {
        public int Id { get; set; }
        public int IdEstudio { get; set; } = 1;
        public string NombreColegio { get; set; } = string.Empty;
        public string CodigoColegio { get; set; } = string.Empty;
        public string Fotografo { get; set; } = string.Empty;
    }

    public class AlumnoRemoteModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int SeccionId { get; set; }
        public string CodigoColegio { get; set; } = string.Empty;
        public int NumeroOrden { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Completado { get; set; } = "NO";
        private string _hobbies = string.Empty;
        public string Hobbies
        {
            get => _hobbies;
            set
            {
                _hobbies = value;
                OnPropertyChanged();
                // 🚀 Sincroniza al mismo tiempo tu estrella de la pestaña reportes
                OnPropertyChanged(nameof(HobbiesFormateado));
            }
        }
        private string _comidaFav = string.Empty;
        public string ComidaFav
        {
            get => _comidaFav;
            set
            {
                _comidaFav = value;
                OnPropertyChanged();
                // 🚀 Sincroniza al mismo tiempo tu icono de comida de la pestaña reportes
                OnPropertyChanged(nameof(ComidaFormateada));
            }
        }
        private string _profesion = string.Empty;
        public string Profesion
        {
            get => _profesion;
            set
            {
                _profesion = value;
                OnPropertyChanged();
            }
        }

        private DateTime? _fechaNacimiento;
        public DateTime? FechaNacimiento
        {
            get => _fechaNacimiento;
            set
            {
                _fechaNacimiento = value;
                OnPropertyChanged();

                // Sincroniza los 3 bloques nuevos cuando se carga la BD o usas el calendario
                OnPropertyChanged(nameof(SurfaceDia));
                OnPropertyChanged(nameof(SurfaceMes));
                OnPropertyChanged(nameof(SurfaceAnio));
            }
        }

        // 🟢 REGISTRO TRI-BLOQUE CON SINCRONIZACIÓN INMEDIATA EN CALIENTE (ANTI-AMNESIA)
        private bool _isSincronizandoBloques = false;
        private string _diaTemp = "dd";
        private string _mesTemp = "mm";
        private string _anioTemp = "aaaa";

        public string SurfaceDia
        {
            get => FechaNacimiento.HasValue ? FechaNacimiento.Value.Day.ToString("00") : _diaTemp;
            set { _diaTemp = value; ActualizarFechaDesdeBloques(value, SurfaceMes, SurfaceAnio); OnPropertyChanged(); }
        }

        public string SurfaceMes
        {
            get => FechaNacimiento.HasValue ? FechaNacimiento.Value.Month.ToString("00") : _mesTemp;
            set { _mesTemp = value; ActualizarFechaDesdeBloques(SurfaceDia, value, SurfaceAnio); OnPropertyChanged(); }
        }

        public string SurfaceAnio
        {
            get => FechaNacimiento.HasValue ? FechaNacimiento.Value.Year.ToString("0000") : _anioTemp;
            set { _anioTemp = value; ActualizarFechaDesdeBloques(SurfaceDia, SurfaceMes, value); OnPropertyChanged(); }
        }

        private void ActualizarFechaDesdeBloques(string d, string m, string a)
        {
            if (_isSincronizandoBloques) return;

            if (string.IsNullOrWhiteSpace(d) || d == "dd" || string.IsNullOrWhiteSpace(m) || m == "mm" || string.IsNullOrWhiteSpace(a) || a == "aaaa")
            {
                _isSincronizandoBloques = true;
                FechaNacimiento = null;
                _isSincronizandoBloques = false;
                return;
            }

            if (int.TryParse(d, out int dia) && int.TryParse(m, out int mes) && int.TryParse(a, out int anio))
            {
                try
                {
                    // Validamos rangos mínimos estándar
                    if (anio >= 1900 && anio <= 2100 && mes >= 1 && mes <= 12 && dia >= 1 && dia <= DateTime.DaysInMonth(anio, mes))
                    {
                        _isSincronizandoBloques = true;

                        // Fijamos la fecha real en la propiedad principal
                        FechaNacimiento = new DateTime(anio, mes, dia);

                        _isSincronizandoBloques = false;
                    }
                }
                catch
                {
                    _isSincronizandoBloques = false;
                }
            }
        }

        private string _sexo = "-";
        public string Sexo
        {
            get => _sexo;
            set
            {
                _sexo = value;
                OnPropertyChanged();
            }
        }

        // Textos de formato para la UI
        public string HobbiesFormateado => $"⭐ Pref: {(string.IsNullOrWhiteSpace(Hobbies) ? "Sin registrar" : Hobbies)}";
        public string ComidaFormateada => $"🍖 Comida: {(string.IsNullOrWhiteSpace(ComidaFav) ? "-" : ComidaFav)}";

        // Constructor: Forzamos a que el alumno nuevo nazca abierto para el padre
        public AlumnoRemoteModel()
        {
            this.AccesoLiberado = 1; // 🟢 Abierto para Padre por defecto
            this.FotoRostro = "";    // Combo apunta a "-- Ninguna --"
            this.FotoFamiliar = "";  // Combo apunta a "-- Ninguna --"
        }

        private int _accesoLiberado;
        public int AccesoLiberado
        {
            get => _accesoLiberado;
            set
            {
                _accesoLiberado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextoEstadoWeb));
                OnPropertyChanged(nameof(ColorEstadoWeb));
                OnPropertyChanged(nameof(TextoBotonPermiso));
                OnPropertyChanged(nameof(ColorBotonPermiso));
            }
        }

        private string _fotoRostro = string.Empty;
        public string FotoRostro
        {
            get => _fotoRostro;
            set
            {
                _fotoRostro = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RutaFotoRostro));
                // 🔑 EL CONECTOR VISUAL: Le dice al ComboBox del DataGrid que cambie su texto en vivo
                OnPropertyChanged(nameof(FotoRostro));
            }
        }

        private string _fotoFamiliar = string.Empty;
        public string FotoFamiliar
        {
            get => _fotoFamiliar;
            set
            {
                _fotoFamiliar = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RutaFotoFamiliar));
                // 🔑 EL CONECTOR VISUAL: Le dice al ComboBox Familiar que cambie su selección en vivo
                OnPropertyChanged(nameof(FotoFamiliar));
            }
        }
        public string Fotografo { get; set; } = string.Empty;

        // Traductores dinámicos inmediatos para las columnas del DataGrid
        public string TextoEstadoWeb => AccesoLiberado == 1 ? "🟢 Abierto para Padre" : "🔴 Bloqueado (Completado)";
        public string ColorEstadoWeb => AccesoLiberado == 1 ? "#28A745" : "#DC3545";
        public string TextoBotonPermiso => AccesoLiberado == 1 ? "🔒 Bloquear Padre" : "🔓 Permitir Acceso";
        public string ColorBotonPermiso => AccesoLiberado == 1 ? "#DC3545" : "#28A745";

        // Enlaces dinámicos absolutos para el pintado de imágenes en filas altas
        public string RutaFotoRostro => ObtenerRutaImagenLocal("rostros", FotoRostro);
        public string RutaFotoFamiliar => ObtenerRutaImagenLocal("familiares", FotoFamiliar);

        // 🟢 CONFIGURACIÓN MAESTRA DE RUTAS EN ESPEJO CON TU SCRIPT PHP
        private string ObtenerRutaImagenLocal(string subCarpeta, string archivo)
        {
            if (string.IsNullOrWhiteSpace(archivo))
            {
                return "PENDIENTE";
            }

            string archivoLimpio = archivo.Trim().ToUpper();

            // Si la celda está vacía o es Ninguna, cortamos el hilo para no mandar basura al servidor
            if (archivoLimpio == "-" || archivoLimpio == "PENDIENTE" || archivoLimpio == "EMERGENCIA" || archivoLimpio == "-- NINGUNA --")
            {
                return "PENDIENTE";
            }

            // 🟢 REPLICACIÓN PHP: Mapeamos la estructura /Fotos/CodigoColegio/tipo/archivo
            // Asegúrate de que subCarpeta reciba "rostros" o "familiares" según corresponda
            string urlFinal = $"https://pixeleduca.com/SmartAnuarios/Fotos/{CodigoColegio.Trim()}/{subCarpeta}/{archivo.Trim()}";

            // 🟢 SILENCIADO PARA ELIMINAR LA RÁFAGA DE TEXTO EN LA CONSOLA (v2026)
            // System.Diagnostics.Debug.WriteLine($"[PHP ESPEJO ➔] Apuntando a ruta real de imagen: {urlFinal}");

            return urlFinal;
        }

        // =======================================================================
        // 🔒 VARIABLE DE CONTROL POR FILA INDIVIDUAL (ANTI-BLOQUEO MASIVO v2026)
        // =======================================================================
        private bool _estaSiendoEditadoLocal = false;
        public bool EstaSiendoEditadoLocal
        {
            get => _estaSiendoEditadoLocal;
            set
            {
                _estaSiendoEditadoLocal = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string TextoItemEstadoWeb()
        {
            if (Completado == "SI" && AccesoLiberado == 0)
                return "FINALIZADO (Cerrado)";
            if (Completado == "SI" && AccesoLiberado == 1)
                return "MODIFICADO (Abierto)";
            return "PENDIENTE (No ingresó)";
        }
                public void NotifyExternalPropertyChange(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        private bool _isFamiliarExcluido = false;
        public bool IsFamiliarExcluido
        {
            get => _isFamiliarExcluido;
            set
            {
                _isFamiliarExcluido = value;
                NotifyExternalPropertyChange(nameof(IsFamiliarExcluido));
            }
        }
    }
}
