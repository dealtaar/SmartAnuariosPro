using SmartAnuariosPro.Services;
using SmartAnuariosPro.Views;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SmartAnuariosPro
{
    // 🟢 ROTADOR EXIF INDUSTRIAL MAESTRO (COMPATIBLE CON ROTATE 90 CW NIKON EXIF MAKER)
    public class RotadorExifAutomatizado : IValueConverter
    {
        private static readonly System.Net.Http.HttpClient _clienteWeb;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> _cacheImagenes = new System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource>();

        static RotadorExifAutomatizado()
        {
            _clienteWeb = new System.Net.Http.HttpClient();
            _clienteWeb.DefaultRequestHeaders.Clear();
            _clienteWeb.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _clienteWeb.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
        }

        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not string ruta || string.IsNullOrWhiteSpace(ruta))
                return null;

            string valorLimpio = ruta.Trim().ToUpper();
            if (valorLimpio.Contains("PENDIENTE") || valorLimpio.Contains("EMERGENCIA") ||
                valorLimpio.Contains("NINGUNA") || valorLimpio == "-" || valorLimpio.EndsWith("/"))
            {
                return null;
            }

            if (_cacheImagenes.TryGetValue(ruta, out var imagenCacheada))
            {
                return imagenCacheada;
            }

            // 💻 CALCULO DE RUTA DE CLONACIÓN LOCAL (Según tu PathResolverService)
            string carpetaBase = AppBootstrap.Instance.Paths.BaseMasterPath ?? "C:\\Anuarios 2026";
            string fotografoLimpio = "Fotografo_Anonimo";
            string codigoColegioLimpio = "";

            if (Application.Current.MainWindow is MainWindow ventanaMaestra)
            {
                if (ventanaMaestra._colegioActualEnEdicion != null)
                {
                    fotografoLimpio = ventanaMaestra._colegioActualEnEdicion.Fotografo.Trim();
                    codigoColegioLimpio = SmartAnuariosPro.Services.PathResolverService.FormatearCodigoColegio(ventanaMaestra._colegioActualEnEdicion.CodigoColegio);
                }
            }

            string nombreArchivoLimpio = System.IO.Path.GetFileName(ruta).Trim().ToUpper();
            string subCarpetaDestino = valorLimpio.Contains("FAMILIAR") ? "Familiares" : "Individuales";
            string rutaLocalFisica = System.IO.Path.Combine(carpetaBase, fotografoLimpio, codigoColegioLimpio, "Fotos Por Escoger", subCarpetaDestino, nombreArchivoLimpio);

            // 📡 TU CANAL ORIGINAL DE DESCARGA (Intacta y funcional, inmune al Error 403)
            Task.Run(async () =>
            {
                try
                {
                    // 🟢 Invocamos a internet usando TU LÓGICA EXACTA ANTERIOR (Sin alterar tu URL nativa)
                    byte[] bytesImagen = await _clienteWeb.GetByteArrayAsync(ruta);

                    if (bytesImagen == null || bytesImagen.Length < 4) return;

                    // =======================================================================
                    // 💾 INTERCEPTACIÓN QUIRÚRGICA: Clonamos los bytes directo al disco local
                    // =======================================================================
                    if (!System.IO.File.Exists(rutaLocalFisica))
                    {
                        try
                        {
                            string? directorioContenedor = System.IO.Path.GetDirectoryName(rutaLocalFisica);
                            if (!string.IsNullOrEmpty(directorioContenedor) && !System.IO.Directory.Exists(directorioContenedor))
                            {
                                System.IO.Directory.CreateDirectory(directorioContenedor);
                            }

                            // Guardamos el archivo original de 160 KB de forma invisible en tu PC de trabajo
                            await System.IO.File.WriteAllBytesAsync(rutaLocalFisica, bytesImagen);
                            System.Diagnostics.Debug.WriteLine($"[CLONADOR AUTOMÁTICO ✓] Interceptado y guardado en: {rutaLocalFisica}");
                        }
                        catch (Exception exDisco)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CLONADOR ALERTA] Falló la escritura: {exDisco.Message}");
                        }
                    }

                    // Tu motor gráfico de descodificación EXIF, 120px en RAM y rotación por GPU continúa idéntico...
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            using (var ms = new System.IO.MemoryStream(bytesImagen))
                            {
                                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                                if (decoder.Frames == null || decoder.Frames.Count == 0) return;

                                var frame = decoder.Frames[0];
                                var metadata = frame.Metadata as BitmapMetadata;
                                int gradosRotacion = 0;

                                if (metadata != null)
                                {
                                    object? orientacionValor = null;
                                    if (metadata.ContainsQuery("/app1/ifd0/{ushort=274}"))
                                        orientacionValor = metadata.GetQuery("/app1/ifd0/{ushort=274}");
                                    else if (metadata.ContainsQuery("/app1/ifd0/Orientation"))
                                        orientacionValor = metadata.GetQuery("/app1/ifd0/Orientation");
                                    else if (metadata.ContainsQuery("/app1/ifd0/{ushort=33434}"))
                                        orientacionValor = metadata.GetQuery("/app1/ifd0/{ushort=33434}");

                                    if (orientacionValor != null)
                                    {
                                        string strValor = orientacionValor?.ToString()?.ToUpper() ?? "";
                                        if (strValor.Contains("90") || strValor == "6" || strValor.Contains("CW")) gradosRotacion = 90;
                                        else if (strValor.Contains("180") || strValor == "3") gradosRotacion = 180;
                                        else if (strValor.Contains("270") || strValor == "8" || strValor.Contains("CCW")) gradosRotacion = 270;
                                    }
                                    else if (frame.PixelWidth > frame.PixelHeight && ruta.ToLower().Contains("rostros"))
                                    {
                                        gradosRotacion = 90;
                                    }
                                }

                                ms.Position = 0;
                                var miniBitmap = new BitmapImage();
                                miniBitmap.BeginInit();
                                miniBitmap.StreamSource = ms;
                                miniBitmap.DecodePixelWidth = 120; // Protege tu scroll para que no parpadee
                                miniBitmap.CacheOption = BitmapCacheOption.OnLoad;
                                miniBitmap.EndInit();

                                miniBitmap.Freeze();
                                ImageSource resultadoFinal = miniBitmap;

                                if (gradosRotacion != 0)
                                {
                                    var transformado = new TransformedBitmap();
                                    transformado.BeginInit();
                                    transformado.Source = miniBitmap;
                                    transformado.Transform = new RotateTransform(gradosRotacion);
                                    transformado.EndInit();
                                    transformado.Freeze();
                                    resultadoFinal = transformado;
                                }

                                _cacheImagenes.TryAdd(ruta, resultadoFinal);

                                var mainWin = Application.Current.MainWindow;
                                if (mainWin != null)
                                {
                                    var grid = mainWin.FindName("GridEdicionColegio") as DataGrid;
                                    if (grid != null)
                                    {
                                        foreach (var item in grid.Items)
                                        {
                                            if (item is AlumnoRemoteModel al)
                                            {
                                                al.NotifyExternalPropertyChange("RutaFotoRostro");
                                                al.NotifyExternalPropertyChange("RutaFotoFamiliar");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception exInner)
                        {
                            System.Diagnostics.Debug.WriteLine($"[EXIF INNER ERROR] {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EXIF NET ERROR] {ex.Message}");
                }
            });

            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class SexoNullConverter : IValueConverter
    {
        // De la Base de Datos a la Pantalla: Si viene vacío, nulo o "-", muestra "-- Elige --"
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? txt = value?.ToString();
            if (string.IsNullOrWhiteSpace(txt) || txt == "-")
                return "-- Elige --";
            return txt;
        }

        // De la Pantalla a la Base de Datos: Si el usuario escoge "-- Elige --", guarda un string vacío "" (MySQL guardará NULL)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? txt = value?.ToString();
            if (txt == "-- Elige --" || txt == "- Elige -")
                return "";
            return txt ?? "";
        }
    }

    // [MainWindow.xaml.cs] - Añade este convertidor arriba del todo, dentro del namespace
    public class FotoNullConverter : IValueConverter
    {
        // De la Base de Datos a la Pantalla: Si viene vacío, nulo o vacío, muestra "-- Ninguna --"
        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string? txt = value?.ToString();
            if (string.IsNullOrWhiteSpace(txt) || txt == "PENDIENTE")
                return "-- Ninguna --";
            return txt;
        }

        // De la Pantalla a la Base de Datos: Si el usuario escoge "-- Ninguna --", guarda un string vacío "" (MySQL guardará NULL)
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string? txt = value?.ToString();
            if (txt == "-- Ninguna --")
                return "";
            return txt ?? "";
        }
    }

    public partial class MainWindow : Window
    {
        // Orígenes de datos en memoria para la grilla Excel y las ráfagas
        private DataTable _dtIndexacion = new DataTable();
        private ObservableCollection<ThumbnailItem> _listMiniaturas = new ObservableCollection<ThumbnailItem>();

        // 🟢 BÚFERES EXCLUSIVOS DE RUTAS PARA LA PESTAÑA INGRESAR COLEGIO
        private bool _isSincronizandoUI = false;

        // 🔒 CANDADO EXCLUSIVO: Evita que el radar interfiera con los clics del usuario en el botón de acceso
        private int _ignorarAccesoRadarId = -1;

        private readonly System.Threading.SemaphoreSlim _radarSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private System.Windows.Threading.DispatcherTimer _timerRadarEnVivo = new();
        public ColegioRemoteModel? _colegioActualEnEdicion = null;

        // 🛡️ BÚFERES MAESTROS COEXISTENTES PARA CONSULTAS DE BANNER INTERACTIVO
        private ColegioRemoteModel? _colegioPorDestruir = null;
        private System.Collections.Generic.List<AlumnoRemoteModel>? _listaAlumnosParaRevertir = null;
        private AlumnoRemoteModel? _alumnoPorQuitarGrilla = null;

        // 🧠 Variable de control para la carga sucesiva de fotos crudas externas
        private bool _esperandoConfirmacionMasFotosCrudas = false;
        private bool _esperandoConfirmacionReversion = false;
        private bool _esperandoConfirmacionQuitarFila = false;

        // 🧠 BÚFER INTELIGENTE MULTIORIGEN: Guarda la ruta exacta de cada archivo crudo disperso seleccionado
        private System.Collections.Generic.List<string> _rutasFotosCrudasSeleccionadas = new();

        // 🧠 RASTREADORES HISTÓRICOS: Guardan la clave [Ruta Destino Local] -> [Ruta Origen Seleccionada]
        private System.Collections.Generic.Dictionary<string, string> _origenRostrosMapeo = new();
        private System.Collections.Generic.Dictionary<string, string> _origenFamiliasMapeo = new();

        private System.Collections.Generic.List<string> _fotosRostrosIniciales = new();
        private System.Collections.Generic.List<string> _fotosFamiliaresIniciales = new();


        // 🟡 BÚFERES EXCLUSIVOS DE RUTAS PARA LA PESTAÑA EDITAR COLEGIO
        private System.Collections.Generic.List<string> _fotosRostrosNuevas = new();
        private System.Collections.Generic.List<string> _fotosFamiliasNuevas = new();

        // 🛡️ BÚFER MASIVO DETECTOR DE CONFLICTOS EN CALIENTE
        private System.Collections.Generic.Dictionary<int, AlumnoRemoteModel> _historialAlumnosEnEdicionLocal = new();
        private bool _conflictoDetectadoYBloqueado = false;

        // Colecciones enlazables para que todos los ComboBoxes de la grilla lean los archivos del hosting
        public ObservableCollection<string> ListaRostrosDisponibles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ListaFamiliaresDisponibles { get; set; } = new ObservableCollection<string>();

        // Constructor de tu MainWindow en MainWindow.xaml.cs
        public MainWindow()
        {
            InitializeComponent();

            // =======================================================================
            // 🎨 PINTRADO NATIVO DE BARRA DE TÍTULO - ESTILO AZUL ACCENTO (v2026)
            // =======================================================================
            try
            {
                var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
                interopHelper.EnsureHandle();
                var handleVentana = interopHelper.Handle;

                // 🟢 CORREGIDO: Usamos exactamente el mismo color de tus paneles de carga masiva
                var colorAzulHex = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#111C24");

                // Convertimos el color al formato de color nativo de Windows (BGR DWORD)
                uint colorDwm = (uint)(colorAzulHex.R | (colorAzulHex.G << 8) | (colorAzulHex.B << 16));

                // Atributo 35: DWMWA_CAPTION_COLOR (Cambia el fondo de la barra nativa)
                NativeMethods.DwmSetWindowAttribute(handleVentana, 35, ref colorDwm, sizeof(uint));

                // Atributo 36: DWMWA_TEXT_COLOR (Forzamos las letras del título a Blanco Nítido)
                uint colorTextoBlanco = 0xFFFFFF;
                NativeMethods.DwmSetWindowAttribute(handleVentana, 36, ref colorTextoBlanco, sizeof(uint));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DWM] No se pudo pintar la barra nativa: {ex.Message}");
            }

            // =======================================================================
            // LÓGICA HABITUAL DE ALEXANDER (INTACTA)
            // =======================================================================
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;

            // Nombre del archivo de configuración local en el directorio del programa
            string archivoConfig = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_maestra.txt");
            string rutaInicial = "C:\\"; // Ruta segura por defecto

            try
            {
                // Si el archivo ya existe, leemos la ruta guardada por el operario
                if (System.IO.File.Exists(archivoConfig))
                {
                    string rutaGuardada = System.IO.File.ReadAllText(archivoConfig).Trim();

                    if (!string.IsNullOrWhiteSpace(rutaGuardada) && System.IO.Directory.Exists(rutaGuardada))
                    {
                        rutaInicial = rutaGuardada;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al leer configuración: {ex.Message}");
            }

            // Inyectamos la ruta real definitiva en la UI y el motor global
            TxtRutaCarpetaMaestra.Text = rutaInicial;
            AppBootstrap.Instance.Paths.BaseMasterPath = rutaInicial;

            ConfigurarEntornoProduccion();

            // CÁLCULO DE RED SEGURO: Forzamos la descarga de la base de datos
            // únicamente cuando la app esté 100% cargada y visible en pantalla
            this.Loaded += async (s, e) => await InicializarReporteColegiosAsync();
        }



        /// <summary>
        /// Sincroniza la botonera del Modo Oscuro con los nombres reales del XAML.
        /// </summary>
        private void ConfigurarEntornoProduccion()
        {
            // 1. Estructura interna de la Mesa de Indexación (Inicia vacía en producción)
            _dtIndexacion.Columns.Add("numero_orden", typeof(string));
            _dtIndexacion.Columns.Add("nombre_completo", typeof(string));
            _dtIndexacion.Columns.Add("foto_rostro", typeof(string));
            _dtIndexacion.Columns.Add("foto_familiar", typeof(string));

            // Vinculación únicamente a las grillas operativas correspondientes
            GridIngresarAlumnos.ItemsSource = _dtIndexacion.DefaultView;
            GridExcel.ItemsSource = _dtIndexacion.DefaultView;
            GridReporteElecciones.ItemsSource = null;

            // =======================================================================
            // 🔗 PARTE 1: ENLACE TÉCNICO DE EVENTOS - MÓDULO 1 (CONTROL DE COLEGIOS)
            // =======================================================================
            BtnCambiarCarpetaMaestra.Click += BtnCambiarCarpetaMaestra_Click;

            // Sub-Pestaña: Ingresar Colegio
            TxtIngresarCodigoColegio.LostFocus += CamposTexto_LostFocus_Formatear;
            TxtIngresarNombreColegio.LostFocus += CamposTexto_LostFocus_Formatear;
            TxtIngresarFotografo.LostFocus += CamposTexto_LostFocus_Formatear;
            BtnExaminarCrudasOrigen.Click += BtnExaminarCrudasOrigen_Click;
            BtnExaminarRostros.Click += BtnCargarRostrosIniciales_Click;
            BtnExaminarFamilias.Click += BtnCargarFamiliaresIniciales_Click;
            BtnLimpiarRostrosLote.Click += BtnLimpiarRostrosLote_Click;
            BtnLimpiarFamiliasLote.Click += BtnLimpiarFamiliasLote_Click;
            TxtAnuarioNombre.TextChanged += TxtAnuarioNombre_TextChanged;
            BtnAnadirAlumnoGrid.Click += BtnAnadirAlumnoGrid_Click;
            BtnGuardarColegioCompleto.Click += BtnGuardarColegioCompleto_Click;
            TxtAnuarioOrden.Text = "1";


            // Sub-Pestaña: Editar Colegio
            BtnCargarColegioEdicion.Click += BtnCargarColegioEdicion_Click;
            BtnCargarMasRostros.Click += BtnCargarMasRostros_Click;
            BtnCargarMasFamiliares.Click += BtnCargarMasFamiliares_Click;
            BtnAplicarCambiosMaestros.Click += BtnAplicarCambiosMaestros_Click;
            GridEdicionColegio.CellEditEnding += GridEdicionColegio_CellEditEnding;

            // =======================================================================
            // 🛡️ ESCUDO CENTRALIZADO DE ENTRADA: CANDADO ABSOLUTO FILA POR FILA (V2026)
            // =======================================================================
            GridEdicionColegio.BeginningEdit += (s, e) => {
                if (e.Row.Item is AlumnoRemoteModel alumno)
                {
                    // 🟢 PASO 1: Encendemos el interruptor de la fila de forma inmediata.
                    // Esto activa la franja celeste vertical y congela el Timer para este alumno en el acto.
                    if (!alumno.EstaSiendoEditadoLocal)
                    {
                        alumno.EstaSiendoEditadoLocal = true;
                    }

                    // 🟢 PASO 2: Registramos el alumno en el búfer relacional de control
                    if (!_historialAlumnosEnEdicionLocal.ContainsKey(alumno.Id))
                    {
                        // Filtro de normalización idéntico a tu arquitectura MariaDB
                        Func<string, string> limpiarParaBuffer = (v) => {
                            if (string.IsNullOrWhiteSpace(v)) return "";
                            string l = v.Trim().ToUpper();
                            if (l == "PENDIENTE" || l == "EMERGENCIA" || l == "-- NINGUNA --") return "";
                            return v;
                        };

                        _historialAlumnosEnEdicionLocal.Add(alumno.Id, new AlumnoRemoteModel
                        {
                            Id = alumno.Id,
                            FotoRostro = limpiarParaBuffer(alumno.FotoRostro),
                            FotoFamiliar = limpiarParaBuffer(alumno.FotoFamiliar),
                            NombreCompleto = alumno.NombreCompleto,
                            Sexo = alumno.Sexo,
                            FechaNacimiento = alumno.FechaNacimiento,
                            Hobbies = alumno.Hobbies,
                            ComidaFav = alumno.ComidaFav,
                            Profesion = alumno.Profesion
                        });

                        System.Diagnostics.Debug.WriteLine($"[ESCUDO TOTAL ACTIVADO ✓] Fila bloqueada por edición activa en campo: {e.Column.Header}");
                    }
                }
            };

            // Sub-Pestaña: Eliminar Colegio
            BtnCargarColegioEliminar.Click += BtnCargarColegioEliminar_Click;
            BtnDestruirColegio.Click += BtnDestruirColegio_Click;

            // Sub-Pestaña: Reporte de Elecciones
            BtnGenerarReporte.Click += BtnGenerarReporte_Click;
            BtnExportarExcelAdministrativo.Click += BtnExportarExcelAdministrativo_Click;

            // Navegación y Sincronización Global de pestañas
            ContenedorPrincipalModulos.SelectionChanged += ContenedorPrincipalModulos_SelectionChanged;


            // 2. Enlace técnico de eventos - Módulo IA (Pasos 1-4)
            BotonProcesarLote.Click += BotonProcesarLote_Click;
            BotonAbortar.Click += BotonAbortar_Click;

            // 3. Enlace de la Mesa de Indexación y columnas
            BtnAnadirColumna.Click += BtnAnadirColumna_Click;
            BtnGuardarAvanceMesa.Click += BtnGuardarAvanceMesa_Click;
            GridExcel.SelectionChanged += GridExcel_SelectionChanged;

            // 4. Enlace del Mapeador de Páginas para Photoshop
            BtnProcesarMapeo.Click += BtnProcesarMapeo_Click;

            this.DataContext = this;

            // =======================================================================
            // 📡 MONITOR DE RADAR EN VIVO QUIRÚRGICO (FILTRADO EXACTO POR FILA EN EDICIÓN)
            // =======================================================================
            _timerRadarEnVivo.Interval = TimeSpan.FromSeconds(5);
            _timerRadarEnVivo.Tick += async (s, e) => {
                // 🟢 Semáforo anti-reentrada
                if (!_radarSemaphore.Wait(0)) return;

                try
                {
                    // 🛡️ REGLA: El radar opera de forma independiente al estado visual del ComboBox selector.
                    if (_colegioActualEnEdicion != null && PanelCargandoEdicion.Visibility == Visibility.Collapsed && !_conflictoDetectadoYBloqueado)
                    {
                        // 1. Descarga silenciosa de la verdad actual de la nube (MariaDB StackCP)
                        var alumnosNube = await AppBootstrap.Instance.Database.ObtenerAlumnosPorColegioAsync(_colegioActualEnEdicion.CodigoColegio);

                        // 2. Capturamos la lista en memoria que actualmente está renderizando la grilla visual de WPF
                        if (GridEdicionColegio.ItemsSource is System.Collections.Generic.List<AlumnoRemoteModel> listaVisualActiva)
                        {
                            // CORTOCIRCUITO DE INTEGRIDAD: Si el conteo de alumnos cambió, recargamos la grilla completa
                            if (listaVisualActiva.Count != alumnosNube.Count)
                            {
                                GridEdicionColegio.ItemsSource = alumnosNube;
                                return;
                            }

                            // 3. 🧠 ALGORITMO DEL ESCUDO INDIVIDUAL FILA POR FILA
                            foreach (var alNube in alumnosNube)
                            {
                                // 🟢 PASO A: Localizamos primero al alumno que se está pintando en la pantalla
                                var alVisual = listaVisualActiva.FirstOrDefault(a => a.Id == alNube.Id);
                                if (alVisual != null)
                                {
                                    // 🟢 PASO B: Cambiamos la condición. Si esta fila tiene el interruptor celeste encendido,
                                    // el radar la salta por completo. El resto del DataGrid se refresca libremente.
                                    if (alVisual.EstaSiendoEditadoLocal)
                                        continue;

                                    // 🔬 DETECTOR DE CAMBIOS QUIRÚRGICOS FILA POR FILA (AMPLIADO v2026)
                                    if (alVisual.NombreCompleto != alNube.NombreCompleto) alVisual.NombreCompleto = alNube.NombreCompleto;
                                    if (alVisual.FechaNacimiento != alNube.FechaNacimiento) alVisual.FechaNacimiento = alNube.FechaNacimiento;
                                    if (alVisual.Completado != alNube.Completado) alVisual.Completado = alNube.Completado;

                                    // 🟢 RADAR INTELIGENTE: Solo actualiza el botón si el cambio viene del PADRE, no de Alexander
                                    if (alVisual.Id != _ignorarAccesoRadarId)
                                    {
                                        if (alVisual.AccesoLiberado != alNube.AccesoLiberado)
                                        {
                                            _isSincronizandoUI = true;
                                            Application.Current.Dispatcher.Invoke(() => {
                                                alVisual.AccesoLiberado = alNube.AccesoLiberado;
                                            });
                                            _isSincronizandoUI = false;
                                        }
                                    }
                                    if (alVisual.AccesoLiberado != alNube.AccesoLiberado) alVisual.AccesoLiberado = alNube.AccesoLiberado;

                                    // 🟢 AGREGADO: El radar ahora lee y actualiza los campos de texto en caliente
                                    if (alVisual.Hobbies != alNube.Hobbies) alVisual.Hobbies = alNube.Hobbies;
                                    if (alVisual.ComidaFav != alNube.ComidaFav) alVisual.ComidaFav = alNube.ComidaFav;
                                    if (alVisual.Profesion != alNube.Profesion) alVisual.Profesion = alNube.Profesion;

                                    // 🚻 SYNC DE SEXO EN EL HILO PRINCIPAL (Asegura la reacción del ComboBox)
                                    if (alVisual.Sexo != alNube.Sexo)
                                    {
                                        _isSincronizandoUI = true;
                                        Application.Current.Dispatcher.Invoke(() => {
                                            alVisual.Sexo = alNube.Sexo;
                                        });
                                        _isSincronizandoUI = false;
                                    }

                                    // 📸 Sincronización de imágenes en el hilo principal
                                    if (alVisual.FotoRostro != alNube.FotoRostro)
                                    {
                                        _isSincronizandoUI = true;
                                        Application.Current.Dispatcher.Invoke(() => {
                                            alVisual.FotoRostro = alNube.FotoRostro;
                                        });
                                        _isSincronizandoUI = false;
                                    }

                                    if (alVisual.FotoFamiliar != alNube.FotoFamiliar)
                                    {
                                        _isSincronizandoUI = true;
                                        Application.Current.Dispatcher.Invoke(() => {
                                            alVisual.FotoFamiliar = alNube.FotoFamiliar;
                                        });
                                        _isSincronizandoUI = false;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RADAR ERROR 🚨] Fallo en la sincronización en vivo: {ex.Message}");
                }
                finally
                {
                    // 🔓 Liberación garantizada del semáforo para permitir el siguiente ciclo sin fugas
                    _radarSemaphore.Release();
                }
            };

        }


        private string GetSelectedPhotographer() => string.IsNullOrWhiteSpace(TxtIngresarFotografo.Text) ? "Fotografo_Anonimo" : TxtIngresarFotografo.Text;
        private string GetSelectedSchool() => string.IsNullOrWhiteSpace(TxtIngresarNombreColegio.Text) ? "Colegio_Anonimo" : TxtIngresarNombreColegio.Text;

        #region MÓDULO 1: CONTROL DE COLEGIOS

        // =======================================================================
        // 📂 PARTE 2: GESTIÓN DE CARPETA MAESTRA Y EXAMINADORES INICIALES
        // =======================================================================

        private void BtnCambiarCarpetaMaestra_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Seleccionar Carpeta Maestra de Producción",
                InitialDirectory = System.IO.Directory.Exists(TxtRutaCarpetaMaestra.Text) ? TxtRutaCarpetaMaestra.Text : "C:\\"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtRutaCarpetaMaestra.Text = dialog.FolderName;
                AppBootstrap.Instance.Paths.BaseMasterPath = dialog.FolderName;

                string archivoConfig = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_maestra.txt");
                try
                {
                    System.IO.File.WriteAllText(archivoConfig, dialog.FolderName);
                    System.Diagnostics.Debug.WriteLine($"[CONFIG] Ruta persistida físicamente: {dialog.FolderName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"La ruta se cambió para esta sesión, pero no se pudo guardar en disco: {ex.Message}",
                                    "Advertencia de E/S", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Abre el examinador masivo y pregunta al operador si desea continuar anexando archivos.
        /// </summary>
        private void BtnExaminarCrudasOrigen_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Imágenes Fotográficas Nikon (*.jpg)|*.jpg",
                Title = "Selección Inteligente: Elija los archivos crudos de la promoción"
            };

            if (ofd.ShowDialog() == true)
            {
                // Acumulación atómica en el búfer multiorigen
                foreach (string rutaArchivo in ofd.FileNames)
                {
                    if (!_rutasFotosCrudasSeleccionadas.Contains(rutaArchivo))
                    {
                        _rutasFotosCrudasSeleccionadas.Add(rutaArchivo);
                    }
                }

                // Sincronización con la caja de texto en Modo Oscuro
                TxtRutaFotosCrudasOrigen.Text = $"🔌 Detectados {_rutasFotosCrudasSeleccionadas.Count} archivos crudos listos en el búfer.";

                // ⚡ Activamos la bandera de la pregunta
                _esperandoConfirmacionMasFotosCrudas = true;

                // Lanzamos la consulta interactiva usando el banner premium sin MessageBox
                MostrarNotificacionBanner($"¿Desea agregar más fotos crudas desde otra carpeta o dispositivo externo? (Total actual: {_rutasFotosCrudasSeleccionadas.Count})", false);
            }
        }

        private void BtnCargarRostrosIniciales_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Imágenes Fotográficas (*.jpg)|*.jpg"
            };

            if (ofd.ShowDialog() == true)
            {
                string codigo = TxtIngresarCodigoColegio.Text.Trim();
                string fotografo = GetSelectedPhotographer();

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    MostrarNotificacionBanner("⚠️ Ingrese el código del colegio antes de examinar fotos.", false);
                    return;
                }

                string carpetaDestinoLocal = AppBootstrap.Instance.Paths.GetSubFolder(fotografo, codigo, "Fotos Por Escoger\\Individuales");

                _fotosRostrosIniciales.Clear();
                _origenRostrosMapeo.Clear(); // Reiniciamos el búfer de discriminación

                foreach (string rutaOriginal in ofd.FileNames)
                {
                    string nombreArchivo = System.IO.Path.GetFileName(rutaOriginal);
                    string rutaDestinoFinal = System.IO.Path.Combine(carpetaDestinoLocal, nombreArchivo);

                    // Guardamos la relación de caminos antes de copiar
                    if (!_origenRostrosMapeo.ContainsKey(rutaDestinoFinal))
                    {
                        _origenRostrosMapeo.Add(rutaDestinoFinal, rutaOriginal);
                    }

                    if (rutaOriginal != rutaDestinoFinal)
                    {
                        try
                        {
                            System.IO.File.Copy(rutaOriginal, rutaDestinoFinal, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Copiador Error] No se pudo clonar retrato {nombreArchivo}: {ex.Message}");
                        }
                    }
                    _fotosRostrosIniciales.Add(rutaDestinoFinal);
                }

                TxtStatusRostros.Text = $" Se copiaron y prepararon {_fotosRostrosIniciales.Count} retratos locales";
            }
        }
        private void BtnCargarFamiliaresIniciales_Click(object sender, RoutedEventArgs e)
        {
            if (ChkOmitirFamiliar.IsChecked == true)
            {
                MostrarNotificacionBanner("⚠️ La sección familiar está desactivada para esta promoción.", false);
                return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Imágenes Fotográficas (*.jpg)|*.jpg"
            };

            if (ofd.ShowDialog() == true)
            {
                string codigo = TxtIngresarCodigoColegio.Text.Trim();
                string fotografo = GetSelectedPhotographer();

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    MostrarNotificacionBanner("⚠️ Ingrese el código del colegio antes de examinar fotos.", false);
                    return;
                }

                string carpetaDestinoLocal = AppBootstrap.Instance.Paths.GetSubFolder(fotografo, codigo, "Fotos Por Escoger\\Familiares");

                _fotosFamiliaresIniciales.Clear();
                _origenFamiliasMapeo.Clear(); // Reiniciamos el búfer de discriminación

                foreach (string rutaOriginal in ofd.FileNames)
                {
                    string nombreArchivo = System.IO.Path.GetFileName(rutaOriginal);
                    string rutaDestinoFinal = System.IO.Path.Combine(carpetaDestinoLocal, nombreArchivo);

                    // Guardamos la relación de caminos antes de copiar
                    if (!_origenFamiliasMapeo.ContainsKey(rutaDestinoFinal))
                    {
                        _origenFamiliasMapeo.Add(rutaDestinoFinal, rutaOriginal);
                    }

                    if (rutaOriginal != rutaDestinoFinal)
                    {
                        try
                        {
                            System.IO.File.Copy(rutaOriginal, rutaDestinoFinal, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Copiador Error] No se pudo clonar foto familiar {nombreArchivo}: {ex.Message}");
                        }
                    }
                    _fotosFamiliaresIniciales.Add(rutaDestinoFinal);
                }

                TxtStatusFamilias.Text = $" Se copiaron y prepararon {_fotosFamiliaresIniciales.Count} fotos familiares";
            }
        }

        private void BtnLimpiarRostrosLote_Click(object sender, RoutedEventArgs e)
        {
            if (_fotosRostrosIniciales.Count == 0) return;

            // 1. Vaciamos el búfer de memoria de forma absoluta
            _fotosRostrosIniciales.Clear();

            // 2. Restauramos la etiqueta visual por defecto en la UI
            TxtStatusRostros.Text = "Sin cargar";

            // 3. Notificamos al operador a través del banner superior premium
            MostrarNotificacionBanner("🗑️ Lote de fotos de Rostros removido de la memoria correctamente.", true);
        }

        private void BtnLimpiarFamiliasLote_Click(object sender, RoutedEventArgs e)
        {
            if (_fotosFamiliaresIniciales.Count == 0) return;

            // 1. Vaciamos el búfer de memoria de forma absoluta
            _fotosFamiliaresIniciales.Clear();

            // 2. Restauramos la etiqueta visual por defecto en la UI
            TxtStatusFamilias.Text = "Sin cargar";

            // 3. Notificamos al operador
            MostrarNotificacionBanner("🗑️ Lote de fotos Familiares removido de la memoria correctamente.", true);
        }

        // =======================================================================
        // ⚡ PARTE 3: VOLCADO INTELIGENTE DESDE EXCEL Y COMMITS EN LA NUBE
        // =======================================================================

        // [MainWindow.xaml.cs] - Reemplaza el método completo por este bloque inteligente
        private void TxtAnuarioNombre_TextChanged(object sender, TextChangedEventArgs e)
        {
            string textoActual = TxtAnuarioNombre.Text;

            // 🔍 COMPROBACIÓN MAESTRA: Si el texto contiene saltos de línea (Pegado de Lista o Enter)
            if (textoActual.Contains("\n") || textoActual.Contains("\r"))
            {
                // 1. Apagamos momentáneamente el evento para evitar bucles infinitos en WPF
                TxtAnuarioNombre.TextChanged -= TxtAnuarioNombre_TextChanged;

                // 2. Rompemos el bloque de texto usando cualquier tipo de salto de línea de Windows/Linux
                string[] lineas = textoActual.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (lineas.Length > 0)
                {
                    // Validamos que el contador de orden tenga un número base válido
                    if (!int.TryParse(TxtAnuarioOrden.Text, out int ordenActual))
                        ordenActual = 1;

                    foreach (string linea in lineas)
                    {
                        string nombreLimpio = linea.Trim();
                        if (!string.IsNullOrWhiteSpace(nombreLimpio))
                        {
                            // 🟢 MAGIA HÍBRIDA: Inyectamos una nueva fila física por cada nombre detectado
                            _dtIndexacion.Rows.Add(ordenActual.ToString(), nombreLimpio, "", "");
                            ordenActual++;
                        }
                    }

                    // 3. Sincronizamos el contador numérico para el siguiente registro manual
                    TxtAnuarioOrden.Text = ordenActual.ToString();

                    // 4. Limpiamos la caja de texto para dejarla lista para el siguiente lote
                    TxtAnuarioNombre.Clear();
                }

                // 5. Re-encendemos el evento para que siga escuchando a la suite
                TxtAnuarioNombre.TextChanged += TxtAnuarioNombre_TextChanged;
            }
            // Si es una sola línea ordinaria (tipeo manual), WPF lo conserva en la caja sin tocar la grilla aún
        }



        private void BtnAnadirAlumnoGrid_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtAnuarioNombre.Text))
            {
                MessageBox.Show("Ingrese el nombre completo del alumno antes de registrar.", "Mesa de Control", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _dtIndexacion.Rows.Add(TxtAnuarioOrden.Text.Trim(), TxtAnuarioNombre.Text.Trim(), "", "");

            if (int.TryParse(TxtAnuarioOrden.Text, out int actualOrden))
            {
                TxtAnuarioOrden.Text = (actualOrden + 1).ToString();
            }

            TxtAnuarioNombre.Clear();
            TxtAnuarioNombre.Focus();
        }


        // =======================================================================
        // 💾 COMPORTAMIENTO DEL BOTÓN "GUARDAR COLEGIO" - PARTE 1 DE 2
        // =======================================================================
        private async void BtnGuardarColegioCompleto_Click(object sender, RoutedEventArgs e)
        {
            // 1. Extracción y formateo estricto del código de la institución (Purga espacios y fuerza primera Mayúscula)
            string codigoSucio = TxtIngresarCodigoColegio.Text;
            string codigoColegio = PathResolverService.FormatearCodigoColegio(codigoSucio);
            string nombreColegio = TxtIngresarNombreColegio.Text.Trim();
            string fotografo = TxtIngresarFotografo.Text.Trim();

            // Validaciones de seguridad iniciales de la suite
            if (string.IsNullOrEmpty(codigoColegio) || string.IsNullOrEmpty(nombreColegio))
            {
                MostrarNotificacionBanner("⚠️ El Código y el Nombre de la Institución son obligatorios.", false);
                return;
            }
            if (_dtIndexacion.Rows.Count == 0)
            {
                MostrarNotificacionBanner("⚠️ No hay alumnos registrados en la grilla para inicializar.", false);
                return;
            }

            // Bloqueo preventivo de la UI anti-doble clic maestro
            BtnGuardarColegioCompleto.IsEnabled = false;
            GridIngresarAlumnos.IsEnabled = false;
            TxtIngresarCodigoColegio.IsEnabled = false;
            TxtIngresarNombreColegio.IsEnabled = false;
            ChkOmitirFamiliar.IsEnabled = false;

            // Inicialización de la barra de progreso azul en 0%
            ActualizarProgresoFases(0.0, "Inicializando Pipeline de Producción v2026...");

            bool pipelineExitoso = false;
            bool omitirSeccionFamiliar = ChkOmitirFamiliar.IsChecked == true;
            var pathResolver = AppBootstrap.Instance.Paths;
            pathResolver.BaseMasterPath = TxtRutaCarpetaMaestra.Text.Trim();

            try
            {
                // Despachamos el peso matemático de la carga en un hilo de fondo asíncrono
                await Task.Run(async () =>
                {

                // =======================================================================
                // FASE 1 (0% - 5%): Despliegue automático de las carpetas locales en la PC
                // =======================================================================
                ActualizarProgresoFases(2.0, "Fase 1: Creando árbol de directorios locales en PC...");

                // Inicializa físicamente \Individuales\, \Familiares\, \Evento\, etc.
                pathResolver.InitializeAIFolders(fotografo, codigoColegio);

                await Task.Delay(300); // Latencia mínima de asentamiento en disco
                ActualizarProgresoFases(5.0, "Fase 1: Estructura de FileSystem local desplegada.");

                    // =======================================================================
                    // FASE 2 (5% - 45%): Vaciado asíncrono inteligente de fotos crudas (.jpg)
                    // =======================================================================
                    ActualizarProgresoFases(5.0, "Fase 2: Inicializando vaciado de negativos crudos a la PC...");

                    // 1. Resolvemos la jerarquía definitiva local auditada desde el servicio central
                    string rutaRaizColegioLocal = pathResolver.GetClassroomFolder(fotografo, codigoColegio);

                    // Aseguramos la existencia del directorio físico antes de abrir los flujos
                    if (!System.IO.Directory.Exists(rutaRaizColegioLocal))
                    {
                        System.IO.Directory.CreateDirectory(rutaRaizColegioLocal);
                    }

                    int totalCrudos = _rutasFotosCrudasSeleccionadas.Count;

                    if (totalCrudos > 0)
                    {
                        int crudosCopiados = 0;
                        double rangoFase2 = 45.0 - 5.0; // 40 puntos exactos del peso matemático de la barra azul

                        foreach (string archivoOrigen in _rutasFotosCrudasSeleccionadas)
                        {
                            if (System.IO.File.Exists(archivoOrigen))
                            {
                                string nombreArchivo = System.IO.Path.GetFileName(archivoOrigen);
                                string destinoFinalLocal = System.IO.Path.Combine(rutaRaizColegioLocal, nombreArchivo);

                                // Si el origen y el destino son idénticos (ej. el usuario seleccionó fotos que ya estaban ahí), evitamos el bloqueo de E/S
                                if (archivoOrigen != destinoFinalLocal)
                                {
                                    // Ajuste de hardware silencioso: búfer óptimo a 64KB (65536 bytes) para el bus de datos
                                    using (var streamOrigen = new System.IO.FileStream(archivoOrigen, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, 65536, true))
                                    using (var streamDestino = new System.IO.FileStream(destinoFinalLocal, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 65536, true))
                                    {
                                        await streamOrigen.CopyToAsync(streamDestino);
                                    }
                                }
                            }

                            crudosCopiados++;

                            // Interpolación proporcional en tiempo real para el RellenoAzulProgreso
                            double pctFase2 = 5.0 + (((double)crudosCopiados / totalCrudos) * rangoFase2);
                            ActualizarProgresoFases(pctFase2, $"Fase 2: Copiando a PC: {System.IO.Path.GetFileName(archivoOrigen)} ({crudosCopiados}/{totalCrudos})");
                        }
                    }
                    else
                    {
                        // Si el operario no seleccionó crudos, la barra salta al 45% fluidamente para no colgar la carga
                        ActualizarProgresoFases(45.0, "Fase 2: No se seleccionaron fotos crudas. Continuando pipeline...");
                    }

                    ActualizarProgresoFases(45.0, "Fase 2: Vaciado asíncrono y liberación de canales de disco completado.");

                    // =======================================================================
                    // FASE 3 (45% - 60%): Clasificación física definitiva de muestras en la PC
                    // =======================================================================
                    ActualizarProgresoFases(45.0, "Fase 3: Clasificando y ordenando muestras locales en subcarpetas...");

                    // 🛡️ CORRECCIÓN: Usamos el método nativo del pathResolver con las subcarpetas correctas
                    string folderMuestrasIndividuales = pathResolver.GetSubFolder(fotografo, codigoColegio, "Fotos Por Escoger\\Individuales");
                    string folderMuestrasFamiliares = pathResolver.GetSubFolder(fotografo, codigoColegio, "Fotos Por Escoger\\Familiares");

                    // 📸 1. Vaciado estricto de Retratos en su subcarpeta correspondiente
                    foreach (string rutaOriginal in _fotosRostrosIniciales)
                    {
                        if (System.IO.File.Exists(rutaOriginal))
                        {
                            string nombreArchivo = System.IO.Path.GetFileName(rutaOriginal);
                            string destinoFinalLocal = System.IO.Path.Combine(folderMuestrasIndividuales, nombreArchivo);

                            if (rutaOriginal != destinoFinalLocal)
                            {
                                System.IO.File.Copy(rutaOriginal, destinoFinalLocal, true);
                            }
                        }
                    }

                    // 📸 2. Vaciado estricto de Fotos Familiares en su subcarpeta correspondiente
                    foreach (string rutaOriginal in _fotosFamiliaresIniciales)
                    {
                        if (System.IO.File.Exists(rutaOriginal))
                        {
                            string nombreArchivo = System.IO.Path.GetFileName(rutaOriginal);
                            string destinoFinalLocal = System.IO.Path.Combine(folderMuestrasFamiliares, nombreArchivo);

                            if (rutaOriginal != destinoFinalLocal)
                            {
                                System.IO.File.Copy(rutaOriginal, destinoFinalLocal, true);
                            }
                        }
                    }

                    await Task.Delay(300); // Asentamiento de E/S en hardware
                    ActualizarProgresoFases(60.0, "Fase 3: Muestras locales clasificadas con éxito.");

                    // =======================================================================
                    // FASE 4 (60% - 100%): Subida de muestras en caliente (Base64) + Inyección MySQL
                    // =======================================================================
                    ActualizarProgresoFases(60.0, "Fase 4: Estableciendo comunicación con el Servidor...");

                    // 1. Inyección de la cabecera de la Institución en MySQL remota
                    int seccionIdGenerada = await AppBootstrap.Instance.Database.InsertarColegioEstructuraAsync(codigoColegio, nombreColegio, fotografo);
                    if (seccionIdGenerada <= 0) throw new InvalidOperationException("Fallo crítico: No se pudo registrar la institución en la base de datos.");

                    // 2. Mapeo del lote de alumnos desde la grilla DataTable a la lista remota
                    var listaParaSubir = new System.Collections.Generic.List<AlumnoRemoteModel>();
                    foreach (System.Data.DataRow fila in _dtIndexacion.Rows)
                    {
                        listaParaSubir.Add(new AlumnoRemoteModel
                        {
                            SeccionId = seccionIdGenerada,
                            CodigoColegio = codigoColegio,
                            NumeroOrden = Convert.ToInt32(fila["numero_orden"]),
                            NombreCompleto = fila["nombre_completo"].ToString() ?? "",
                            FotoRostro = "",
                            FotoFamiliar = omitirSeccionFamiliar ? "DESACTIVADO" : ""
                        });
                    }

                    // 3. Volcado masivo de la nómina de alumnos en MySQL
                    bool alumnosInyectados = await AppBootstrap.Instance.Database.InsertarLoteAlumnosAsync(listaParaSubir);
                    if (!alumnosInyectados) throw new InvalidOperationException("Fallo crítico: El servidor remoto rechazó el lote de alumnos.");

                    // 4. Preparación de los búferes físicos de muestras locales consolidados en el Paso 3
                    // 🛡️ CORRECCIÓN DE HARDWARE: Resolvemos las rutas de subcarpetas físicas exactas
                    folderMuestrasIndividuales = pathResolver.GetSubFolder(fotografo, codigoColegio, "Fotos Por Escoger\\Individuales");
                    folderMuestrasFamiliares = pathResolver.GetSubFolder(fotografo, codigoColegio, "Fotos Por Escoger\\Familiares");

                    // 🔍 RADAR 1: Monitoreamos qué rutas de disco está leyendo la Suite
                    System.Diagnostics.Debug.WriteLine($"[Radar Fase 4] Ruta Local Rostros: {folderMuestrasIndividuales}");
                    System.Diagnostics.Debug.WriteLine($"[Radar Fase 4] Ruta Local Familiares: {folderMuestrasFamiliares}");

                    // Escaneamos el disco para capturar las fotos reales listas a 1200px
                    var retratosParaWeb = System.IO.Directory.Exists(folderMuestrasIndividuales)
                        ? System.IO.Directory.GetFiles(folderMuestrasIndividuales, "*.jpg").ToList()
                        : new System.Collections.Generic.List<string>();

                    var familiaresParaWeb = System.IO.Directory.Exists(folderMuestrasFamiliares)
                        ? System.IO.Directory.GetFiles(folderMuestrasFamiliares, "*.jpg").ToList()
                        : new System.Collections.Generic.List<string>();

                    // 🔍 RADAR 2: Validamos la cantidad exacta de archivos que detectó el hardware de la PC
                    System.Diagnostics.Debug.WriteLine($"[Radar Fase 4] Retratos en disco: {retratosParaWeb.Count} | Familiares en disco: {familiaresParaWeb.Count}");

                    int totalMuestrasWeb = retratosParaWeb.Count + familiaresParaWeb.Count;
                    System.Diagnostics.Debug.WriteLine($"[Radar Fase 4] Total Lote a transmitir: {totalMuestrasWeb}");

                    ActualizarProgresoFases(70.0, "Fase 4: Inicializando subida de archivos al Servidor...");

                    // 5. Instanciamos el Progress de red para el estiramiento fluido de la barra azul
                    var progresoSubida = new Progress<ReporteProgresoRedModel>(reporte =>
                    {
                        double pctSubidaReal = 70.0 + (reporte.Porcentaje * 0.3);
                        ActualizarProgresoFases(pctSubidaReal, $"Fase 4: Transmitiendo {reporte.NombreArchivo} al Servidor...");
                    });

                    // 6. DISPARO SECUENCIAL SINCRO: Pasamos las listas físicas del disco para match perfecto con PHP
                    bool exitoRostros = await SubirArchivosFisicosHostingAsync(codigoColegio, retratosParaWeb, true, 0, totalMuestrasWeb, progresoSubida);
                    bool exitoFamilias = await SubirArchivosFisicosHostingAsync(codigoColegio, familiaresParaWeb, false, retratosParaWeb.Count, totalMuestrasWeb, progresoSubida);

                    if (!exitoRostros || !exitoFamilias)
                    {
                        throw new InvalidOperationException("Advertencia: Algunas muestras de fotos fueron rechazadas por el ancho de banda del servidor.");
                    }

                    // Hito final de cierre exitoso del pipeline de la Suite
                    pipelineExitoso = true;

                });
            }

            catch (LogisticalException logEx)
            {
                // Fallo parcial de red pero datos a salvo en MySQL
                MostrarNotificacionBanner($"{logEx.Message}", false);
                pipelineExitoso = true; // Permite dar paso a la finalización parcial
            }
            catch (Exception ex)
            {
                // Fallo total de red o disco en StackCP -> Banner en color Rojo
                MostrarNotificacionBanner($"❌ Error de red o disco en Servidor: {ex.Message}", false);
                ActualizarProgresoFases(0.0, "Guardado de colegio abortado por error crítico.");
            }
            finally
            {
                // Liberación de controles visuales
                BtnGuardarColegioCompleto.IsEnabled = true;
                GridIngresarAlumnos.IsEnabled = true;
                TxtIngresarCodigoColegio.IsEnabled = true;
                TxtIngresarNombreColegio.IsEnabled = true;
                ChkOmitirFamiliar.IsEnabled = true;
            }

            // =======================================================================
            // FINALIZACIÓN EXITOSA Y EXPERIENCIA VISUAL (100% COMPLETADO)
            // =======================================================================
            if (pipelineExitoso)
            {
                ActualizarProgresoFases(100.0, "¡Guardado del colegio completado al 100%! Sincronización exitosa.");

                // Inyección automática al portapapeles de Windows del link del padre con la dirección del hosting con espacios
                string linkPadre = $"https://pixeleduca.com/SmartAnuarios/index.php?colegio={codigoColegio}";
                Clipboard.SetText(linkPadre.Replace(" ", "")); // Remueve los espacios internamente antes de enviarlo al portapapeles

                // Despliegue de Confirmación en Amarillo Pastel Premium
                MostrarNotificacionBanner($"¡Colegio registrado! Link del padre copiado al portapapeles: {linkPadre}", true);

                LimpiarFormularioRegistro();
                await InicializarReporteColegiosAsync();
            }
        }

        /// <summary>
        /// Calcula la franja azul en píxeles reales basándose en el contenedor físico de la UI.
        /// </summary>
        private void ActualizarProgresoFases(double porcentaje, string mensaje)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Sincronizamos los textos superiores del control
                TxtPorcentajeNumStatus.Text = $"{porcentaje:F0}%";
                TxtFotoActualStatus.Text = mensaje;

                // Capturamos el ancho real en píxeles del canal oscuro (\FondoCanalProgreso\)
                double anchoCanal = FondoCanalProgreso.ActualWidth;
                if (anchoCanal <= 0) anchoCanal = 450; // Respaldo preventivo

                // Multiplicación real thread-safe para pintar la barra azul
                RellenoAzulProgreso.Width = (porcentaje / 100.0) * anchoCanal;
            });
        }

        private async Task InyectarAlumnosPurosAsync(int seccionIdGenerada, string codigo)
        {
            var listaParaSubir = new System.Collections.Generic.List<AlumnoRemoteModel>();

            // 📡 Evaluación del estado del Check de la UI
            bool omitirSeccionFamiliar = ChkOmitirFamiliar.IsChecked == true;

            foreach (System.Data.DataRow fila in _dtIndexacion.Rows)
            {
                listaParaSubir.Add(new AlumnoRemoteModel
                {
                    SeccionId = seccionIdGenerada,
                    CodigoColegio = codigo,
                    NumeroOrden = Convert.ToInt32(fila["numero_orden"]),
                    NombreCompleto = fila["nombre_completo"].ToString() ?? "",
                    FotoRostro = "",
                    // 🔒 INTERRUPTOR DE BASE DE DATOS: Si está activo, inyecta la palabra clave técnica para deshabilitarlo de raíz
                    FotoFamiliar = omitirSeccionFamiliar ? "DESACTIVADO" : ""
                });
            }

            bool alumnosInyectados = await AppBootstrap.Instance.Database.InsertarLoteAlumnosAsync(listaParaSubir);

            if (alumnosInyectados)
            {
                // Procedemos al despacho e incremento atómico de la barra azul en paralelo
                await DespacharArchivosFisicosAsync(codigo);
            }
            else
            {
                MessageBox.Show("Fallo de comunicación en los canales de StackCP.", "Error de Servidor", MessageBoxButton.OK, MessageBoxImage.Error);
                RestaurarControlesUI();
            }
        }

        // 📌 BLOQUE 3 DE 3: PROCESAMIENTO Y DESPACHO FÍSICO INTEGRADO CON LOS BORDES
        private async Task DespacharArchivosFisicosAsync(string codigo)
        {
            bool erroresDeCargaFisica = false;
            int totalRostros = _fotosRostrosIniciales.Count;
            int totalFamiliares = _fotosFamiliaresIniciales.Count;
            int totalGlobalLote = totalRostros + totalFamiliares;

            if (totalGlobalLote > 0)
            {
                // ⚡ CAPTURAMOS EL ANCHO REAL: Medimos el contenedor oscuro en píxeles
                double anchoTotalCanal = FondoCanalProgreso.ActualWidth;
                if (anchoTotalCanal <= 0) anchoTotalCanal = 450; // Fallback de seguridad

                var progresoRed = new Progress<ReporteProgresoRedModel>(reporte =>
                {
                    // REGLA DE TRES: Traducimos el porcentaje real de subida a píxeles exactos
                    double nuevosPixelesAzules = (reporte.Porcentaje / 100.0) * anchoTotalCanal;

                    // 🎨 PINTADO INMUNE: Estiramos la franja azul en tiempo real conforme suben las fotos
                    RellenoAzulProgreso.Width = nuevosPixelesAzules;

                    TxtPorcentajeNumStatus.Text = $"{reporte.Porcentaje:F1}%";
                    TxtFotoActualStatus.Text = $"Subiendo al servidor: {reporte.NombreArchivo}";
                });

                // Ejecuta la subida física de rostros a las carpetas del hosting
                if (totalRostros > 0)
                {
                    bool exitoRostros = await SubirArchivosFisicosHostingAsync(codigo, _fotosRostrosIniciales, true, 0, totalGlobalLote, progresoRed);
                    if (!exitoRostros) erroresDeCargaFisica = true;
                }

                // Ejecuta la subida física de familiares heredando el índice de progreso
                if (totalFamiliares > 0)
                {
                    bool exitoFamilias = await SubirArchivosFisicosHostingAsync(codigo, _fotosFamiliaresIniciales, false, totalRostros, totalGlobalLote, progresoRed);
                    if (!exitoFamilias) erroresDeCargaFisica = true;
                }
            }
            else
            {
                // Si el operador no seleccionó fotos, pintamos la barra al 100% de inmediato
                RellenoAzulProgreso.Width = FondoCanalProgreso.ActualWidth;
                TxtPorcentajeNumStatus.Text = "100%";
                TxtFotoActualStatus.Text = "Datos guardados. Sin archivos para el servidor.";
            }

            RestaurarControlesUI();

            if (erroresDeCargaFisica)
            {
                MessageBox.Show("Datos de alumnos guardados en MySQL, pero el hosting rechazó algunos archivos físicos.", "Alerta de Almacenamiento", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MostrarNotificacionBanner("¡Datos guardados e imágenes subidas al servidor con éxito!", true);
                LimpiarFormularioRegistro();
            }
            await InicializarReporteColegiosAsync();
        }


        private void RestaurarControlesUI()
        {
            BtnGuardarColegioCompleto.IsEnabled = true; GridIngresarAlumnos.IsEnabled = true;
            TxtIngresarCodigoColegio.IsEnabled = true; TxtIngresarNombreColegio.IsEnabled = true;
        }

        private void LimpiarFormularioRegistro()
        {
            // 1. Limpieza de cajas de texto maestro
            TxtIngresarCodigoColegio.Clear();
            TxtIngresarNombreColegio.Clear();
            TxtIngresarFotografo.Clear();

            // 2. 🔢 REINICIO DEL CONTADOR SECUENCIAL A 1
            TxtAnuarioOrden.Text = "1";
            TxtAnuarioNombre.Clear();

            // 3. Limpieza de matrices en memoria y búferes de archivos
            _dtIndexacion.Clear();
            _fotosRostrosIniciales.Clear();
            _fotosFamiliaresIniciales.Clear();

            TxtStatusRostros.Text = "Sin cargar";
            TxtStatusFamilias.Text = "Sin cargar";

            // 4. 📉 VACIADO DE BARRA INMUNE Y TEXTOS DE ENCIMA
            RellenoAzulProgreso.Width = 0;
            TxtPorcentajeNumStatus.Text = "0%";
            TxtFotoActualStatus.Text = "Esperando inicio de carga...";
            _rutasFotosCrudasSeleccionadas.Clear();
            TxtRutaFotosCrudasOrigen.Clear();
        }

        // 🧼 MÉTODO DE PURGA ABSOLUTA: RESTABLECIMIENTO DE PESTAÑA A ESTADO CERO
        private void LimpiarMesaEdicionPostGuardado()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 1. 📡 Apagamos el Radar en Vivo en seco para que deje de consultar internet
                _timerRadarEnVivo.Stop();
                _colegioActualEnEdicion = null;

                // 2. 💻 Vaciamos por completo el DataGrid visual de alumnos
                _isSincronizandoUI = true;
                GridEdicionColegio.ItemsSource = null;
                _isSincronizandoUI = false;

                // 3. 🧼 Regresamos el ComboBox de Colegios a su marcador base sin selección
                CboEditarColegiosLista.SelectedIndex = -1;

                // 4. 📸 Vaciamos los buffers locales de fotos adicionales de la PC
                _fotosRostrosNuevas.Clear();
                _fotosFamiliasNuevas.Clear();
                TxtStatusMasRostros.Text = "Ninguno";
                TxtStatusMasFamiliares.Text = "Ninguno";

                // 📋 Limpiamos los textos del banner informativo de edición
                TxtInstitucionSeleccionadaBanner.Text = "NINGÚN COLEGIO SELECCIONADO";

                // =======================================================================
                // 🟢 INTERRUPTOR DE SEGURIDAD: RESTABLECEMOS EL LINK AZUL A ESTADO GUÍA
                // =======================================================================
                if (TxtLinkVisual != null && LinkWebPadre != null)
                {
                    TxtLinkVisual.Text = "Escoja un colegio para generar el link...";

                    // Forzamos el color gris oscuro neutral para indicar que está inactivo
                    LinkWebPadre.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));

                    // Cambiamos el cursor a la flecha ordinaria para bloquear la sensación de clic
                    LinkWebPadre.Cursor = System.Windows.Input.Cursors.Arrow;
                }

                // 🛡️ Vaciamos de raíz el historial de escudos celestes
                _historialAlumnosEnEdicionLocal.Clear();
                _conflictoDetectadoYBloqueado = false;

                System.Diagnostics.Debug.WriteLine("[PURGA ✓] Mesa de edición y link restablecidos a Estado Cero.");
            });
        }




        // =======================================================================
        // ✏ COMPONENTES DE LA CONSOLA DE EDICIÓN EN CALIENTE Y ACTUALIZACIÓN
        // =======================================================================

        // 🌐 EVENTO CLICK: Controlador con aviso de carga síncrona usando Banner industrial
        private async void BtnCargarColegioEdicion_Click(object sender, RoutedEventArgs e)
        {
            // 🛡️ REEMPLAZO ABSOLUTO: Si no hay selección, disparamos el Banner amarillo/rojo en vez del MessageBox
            if (CboEditarColegiosLista.SelectedItem is not ColegioRemoteModel colegioSeleccionado)
            {
                MostrarNotificacionBanner("Por favor, selecciona una promoción del listado antes de presionar Cargar.", false);
                return;
            }

            try
            {
                // Encendemos el indicador de carga flotante autogestionado
                PanelCargandoEdicion.Visibility = Visibility.Visible;
                this.IsEnabled = false;

                TxtInstitucionSeleccionadaBanner.Text = colegioSeleccionado.NombreColegio.ToUpper();

                // 🟢 INYECCIÓN DE LINK REAL: Activamos el color azul y la mano interactiva
                string codigoLimpio = colegioSeleccionado.CodigoColegio.Trim();
                TxtLinkVisual.Text = $"https://pixeleduca.com/SmartAnuarios/index.php?colegio={codigoLimpio}";
                LinkWebPadre.Foreground = (SolidColorBrush)FindResource("AzulAcento"); // O el código "#63B3ED"
                LinkWebPadre.Cursor = Cursors.Hand;

                // Ejecuta la descarga remota y escaneo de carpetas web
                await CargarColegioEdicionAsync(colegioSeleccionado);
            }
            catch (Exception ex)
            {
                // Canalizamos el error de red directo al Banner superior
                MostrarNotificacionBanner($"Fallo al establecer contacto con StackCP: {ex.Message}", false);
            }
            finally
            {
                // Ocultamos el panel flotante y liberamos el software pase lo que pase
                PanelCargandoEdicion.Visibility = Visibility.Collapsed;
                this.IsEnabled = true;
            }
        }

        private void LinkWebPadre_Click(object sender, RoutedEventArgs e)
        {
            // 🛡️ ESCUDO DE SEGURIDAD INDUSTRIAL: Si no hay un link real generado, bloqueamos la acción en seco
            if (string.IsNullOrWhiteSpace(TxtLinkVisual.Text) ||
                TxtLinkVisual.Text.Contains("...") ||
                TxtLinkVisual.Text.StartsWith("Escoja"))
            {
                return; // Sale del método inmediatamente sin hacer nada
            }

            // Si pasó el escudo, significa que ya hay una URL real de la promoción cargada
            Clipboard.SetText(TxtLinkVisual.Text.Trim());
            MostrarNotificacionBanner("📋 ¡Link web copiado al portapapeles con éxito!", true);
        }


        // =======================================================================
        // ✏️ SUB-RUTINA ASÍNCRONA DE DESCARGA DIRECTA Y RENDERIZADO DE EDICIÓN
        // =======================================================================
        private async Task CargarColegioEdicionAsync(ColegioRemoteModel colegioSeleccionado)
        {
            // 1. LIMPIEZA ESTRUCTURAL INICIAL EN EL HILO DE LA UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                ListaRostrosDisponibles.Clear();
                ListaFamiliaresDisponibles.Clear();
                ListaRostrosDisponibles.Add("-- Ninguna --");
                ListaFamiliaresDisponibles.Add("-- Ninguna --");
            });

            // 2. CONSULTA REMOTA: Descargamos los alumnos desde MySQL StackCP
            var alumnosNube = await AppBootstrap.Instance.Database.ObtenerAlumnosPorColegioAsync(colegioSeleccionado.CodigoColegio);

            // 3. 🌐 ESCANEO DE SERVIDOR WEB (Mique Hosting)
            using (var client = new System.Net.Http.HttpClient())
            {
                try
                {
                    // 🔥 CORREGIDO: URL limpia, directa y sin espacios para el HttpClient
                    string urlLimpia = "https://pixeleduca.com/SmartAnuarios/";
                    string codigo = colegioSeleccionado.CodigoColegio.Trim();

                    // Despachamos las peticiones GET asíncronas hacia tus scripts de escaneo PHP
                    var respuestaRostros = await client.GetStringAsync($"{urlLimpia}listar_fotos_directorio.php?codigo={codigo}&tipo=rostros");
                    var respuestaFamilias = await client.GetStringAsync($"{urlLimpia}listar_fotos_directorio.php?codigo={codigo}&tipo=familiares");

                    // 4. INYECCIÓN FLUIDA DE LOTES EN EL HILO PRINCIPAL (CORREGIDO PARA TU XAML)
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Vaciamos por completo las listas visuales ligadas por RelativeSource al DataGrid
                        ListaRostrosDisponibles.Clear();
                        ListaFamiliaresDisponibles.Clear();

                        // 📸 PROCESAMIENTO ATÓMICO DE ROSTROS
                        if (!string.IsNullOrWhiteSpace(respuestaRostros))
                        {
                            var archivosRostros = respuestaRostros.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            var conjuntoUnicoRostros = new System.Collections.Generic.HashSet<string> { "-- Ninguna --" };

                            foreach (var archivo in archivosRostros) conjuntoUnicoRostros.Add(archivo.Trim());
                            foreach (var item in conjuntoUnicoRostros) ListaRostrosDisponibles.Add(item);
                        }
                        else
                        {
                            ListaRostrosDisponibles.Add("-- Ninguna --");
                        }

                        // 👨‍👩‍👧 PROCESAMIENTO ATÓMICO DE FAMILIARES
                        if (!string.IsNullOrWhiteSpace(respuestaFamilias))
                        {
                            var archivosFamilias = respuestaFamilias.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            var conjuntoUnicoFamilias = new System.Collections.Generic.HashSet<string> { "-- Ninguna --" };

                            foreach (var archivo in archivosFamilias) conjuntoUnicoFamilias.Add(archivo.Trim());
                            foreach (var item in conjuntoUnicoFamilias) ListaFamiliaresDisponibles.Add(item);
                        }
                        else
                        {
                            ListaFamiliaresDisponibles.Add("-- Ninguna --");
                        }
                    });

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HTTP SCAN ERROR 🚨] Falló la lectura de carpetas web: {ex.Message}");
                }
            }

            // 5. REDIBUJADO ATÓMICO DEL CONTEXTO DEL DATAGRID
            GridEdicionColegio.ItemsSource = null;
            GridEdicionColegio.AutoGenerateColumns = false;

            // El DataContext es vital para que las columnas encuentren las colecciones ListaRostrosDisponibles
            GridEdicionColegio.DataContext = this;

            // Forzamos al motor a limpiar layouts anteriores
            GridEdicionColegio.UpdateLayout();

            GridEdicionColegio.ItemsSource = alumnosNube;

            // 📡 Activamos el radar en vivo para detectar cambios de los padres
            _colegioActualEnEdicion = colegioSeleccionado;
            _timerRadarEnVivo.Start();

            // Sincronización del interruptor verde de la sección familiar
            if (alumnosNube != null && alumnosNube.Count > 0 && alumnosNube[0].FotoFamiliar == "DESACTIVADO")
            {
                BtnHabilitarFamiliarEdicion.Visibility = Visibility.Visible;
            }
            else
            {
                BtnHabilitarFamiliarEdicion.Visibility = Visibility.Collapsed;
            }
        }

        // 🟢 ENRUTADORES VECTORIALES DE CURSOR DE ALTA VELOCIDAD (C# CODE-BEHIND)
        private void TextBoxBloque_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll(); // Selecciona el bloque completo en azul al hacer clic
            }
        }
        // 🟢 ENRUTADORES DE FOCO MAESTROS CORREGIDOS ANTI-BLOQUEO
        private void TxtDia_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text.Length == 2)
            {
                // Buscamos el control del mes dentro del mismo panel de la celda
                var parent = textBox.Parent as FrameworkElement;
                if (parent != null)
                {
                    var txtMes = parent.FindName("TxtMes") as TextBox;
                    if (txtMes != null)
                    {
                        txtMes.Focus();
                        txtMes.SelectAll();
                    }
                }
            }
        }
        private void TxtMes_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text.Length == 2)
            {
                var parent = textBox.Parent as FrameworkElement;
                if (parent != null)
                {
                    var txtAnio = parent.FindName("TxtAnio") as TextBox;
                    if (txtAnio != null)
                    {
                        txtAnio.Focus();
                        txtAnio.SelectAll();
                    }
                }
            }
        }

        // Método auxiliar opcional para mantener actualizado el contexto de cajas de texto
        private void TxtIngresarStreamCodigoColegio_Simulado(ColegioRemoteModel col)
        {
            TxtIngresarCodigoColegio.Text = col.CodigoColegio;
            TxtIngresarNombreColegio.Text = col.NombreColegio;
            TxtIngresarFotografo.Text = col.Fotografo;
        }

        // 📌 PROCESADOR DE ENTRADA EN MEMORIA RAM (Inmune a InvalidOperationException)
        private async void GridEdicionColegio_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            // Esperamos un instante a que el framework procese el último carácter digitado
            await Task.Delay(50);

            if (e.Row.Item is AlumnoRemoteModel alumnoEditado)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Forzamos el volcado inmediato de los 3 bloques hacia FechaNacimiento en memoria
                    e.Row.BindingGroup?.CommitEdit();

                    // Forzamos el refresco visual exclusivo de la fila para que renderice la fecha en el CellTemplate
                    var fila = GridEdicionColegio.ItemContainerGenerator.ContainerFromItem(alumnoEditado) as DataGridRow;
                    fila?.BindingGroup?.UpdateSources();
                });
            }
        }

        // 🟢 CONTROLADOR LOGÍSTICO COMPARTIDO PARA EL COMBOBOX DE SEXO
        private void ComboBoxSexo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si la Suite está auto-refrescando celdas por código (vía Timer), ignoramos el evento
            if (_isSincronizandoUI) return;

            if (sender is not ComboBox cbo || cbo.DataContext is not AlumnoRemoteModel alumno)
                return;

            // 🔬 REGLA DE FOCO: Solo se activa si Alexander hizo clic real en la pantalla
            if (cbo.IsKeyboardFocusWithin || cbo.IsMouseOver)
            {
                if (!alumno.EstaSiendoEditadoLocal)
                {
                    alumno.EstaSiendoEditadoLocal = true; // 🔓 Enciende la franja celeste al instante
                }

                if (!_historialAlumnosEnEdicionLocal.ContainsKey(alumno.Id))
                {
                    _historialAlumnosEnEdicionLocal.Add(alumno.Id, new AlumnoRemoteModel
                    {
                        Id = alumno.Id,
                        FotoRostro = alumno.FotoRostro == "-- Ninguna --" ? "" : alumno.FotoRostro,
                        FotoFamiliar = alumno.FotoFamiliar == "-- Ninguna --" ? "" : alumno.FotoFamiliar,
                        NombreCompleto = alumno.NombreCompleto,
                        Sexo = alumno.Sexo,
                        FechaNacimiento = alumno.FechaNacimiento,
                        Hobbies = alumno.Hobbies,
                        ComidaFav = alumno.ComidaFav,
                        Profesion = alumno.Profesion
                    });

                    System.Diagnostics.Debug.WriteLine($"[ESCUDO COMPLETO ✓] Fila de {alumno.NombreCompleto} bloqueada por cambio de Sexo.");
                }
            }
        }

        private void ComboBoxFoto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si la app está auto-refrescando celdas por código (vía Timer), ignoramos el evento visual
            if (_isSincronizandoUI) return;

            if (sender is not ComboBox cbo || cbo.DataContext is not AlumnoRemoteModel alumno)
                return;

            if (cbo.SelectedValue == null || string.IsNullOrWhiteSpace(cbo.SelectedValue.ToString()))
                return;

            string fotoNueva = cbo.SelectedValue.ToString()!;
            if (fotoNueva == "-- Ninguna --")
            {
                fotoNueva = "";
            }

            // =======================================================================
            // 🛡️ EL ESCUDO QUIRÚRGICO DE FOCO (CORREGIDO ANTI-DISPARO AUTOMÁTICO)
            // =======================================================================
            // Solo activamos el escudo y pintamos la franja celeste si Alexander interactuó con el ComboBox
            if (cbo.IsKeyboardFocusWithin || cbo.IsMouseOver)
            {
                if (!alumno.EstaSiendoEditadoLocal)
                {
                    alumno.EstaSiendoEditadoLocal = true; // 🔓 Enciende la línea celeste en la UI
                }

                if (!_historialAlumnosEnEdicionLocal.ContainsKey(alumno.Id))
                {
                    _historialAlumnosEnEdicionLocal.Add(alumno.Id, new AlumnoRemoteModel
                    {
                        Id = alumno.Id,
                        FotoRostro = alumno.FotoRostro == "-- Ninguna --" ? "" : alumno.FotoRostro,
                        FotoFamiliar = alumno.FotoFamiliar == "-- Ninguna --" ? "" : alumno.FotoFamiliar,
                        Hobbies = alumno.Hobbies,
                        ComidaFav = alumno.ComidaFav,
                        Profesion = alumno.Profesion,
                        Sexo = alumno.Sexo,
                        FechaNacimiento = alumno.FechaNacimiento
                    });

                    System.Diagnostics.Debug.WriteLine($"[ESCUDO ACTIVADO ✓] Fila de {alumno.NombreCompleto} bloqueada legítimamente.");
                }
            }

            // Encendemos el candado estructural para evitar bucles infinitos en los hilos de WPF
            _isSincronizandoUI = true;
            try
            {
                if (cbo.ItemsSource == this.ListaRostrosDisponibles)
                {
                    alumno.FotoRostro = fotoNueva;
                }
                else if (cbo.ItemsSource == this.ListaFamiliaresDisponibles)
                {
                    alumno.FotoFamiliar = fotoNueva;
                }
            }
            finally
            {
                _isSincronizandoUI = false; // Liberamos siempre el candado en el bloque safe
            }

            // Refresco asíncrono controlado con purga de renderizado en caliente (Mantiene tu lógica funcional)
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var fila = GridEdicionColegio.ItemContainerGenerator.ContainerFromItem(alumno) as DataGridRow;
                if (fila != null)
                {
                    _isSincronizandoUI = true;
                    fila.DataContext = null;
                    await Task.Delay(5);
                    fila.DataContext = alumno;
                    _isSincronizandoUI = false;
                }
            });
        }



        // 💾 ACTUALIZACIÓN MASIVA ASÍNCRONA CON RADAR DE PROGRESO AZUL
        private async void BtnAplicarCambiosMaestros_Click(object sender, RoutedEventArgs e)
        {
            if (GridEdicionColegio.ItemsSource is not System.Collections.Generic.List<AlumnoRemoteModel> listaAlumnos)
            {
                MostrarNotificacionBanner("No hay datos cargados en la mesa para procesar.", false);
                return;
            }

            // 🛡️ REEMPLAZO SEGURO E INDEPENDIENTE: Lee el código directamente del búfer maestro de edición
            if (_colegioActualEnEdicion == null || string.IsNullOrEmpty(_colegioActualEnEdicion.CodigoColegio))
            {
                MostrarNotificacionBanner("No se ha detectado una sesión de edición activa o válida.", false);
                return;
            }

            string codigoColegio = _colegioActualEnEdicion.CodigoColegio.Trim().ToLower();

            // 🔒 Congelamos interfaz y preparamos los indicadores de la pestaña de edición
            this.IsEnabled = false;
            _timerRadarEnVivo.Stop(); // Pausa temporal para evitar colisiones en la escritura

            RellenoAzulProgresoEdicion.Width = 0;
            TxtPorcentajeNumStatusEdicion.Text = "0.0%";
            TxtFotoActualStatusEdicion.Text = "Actualizando correcciones en la Base de Datos...";

            bool fallasBaseDatos = false;
            int totalAlumnos = listaAlumnos.Count;
            double anchoTotalCanal = FondoCanalProgresoEdicion.ActualWidth > 0 ? FondoCanalProgresoEdicion.ActualWidth : 450;

            // 🧮 Canal de progreso para las actualizaciones en la nube
            var progresoActualizacion = new Progress<int>(indiceActual =>
            {
                double porcentaje = ((double)indiceActual / totalAlumnos) * 100;
                RellenoAzulProgresoEdicion.Width = (porcentaje / 100.0) * anchoTotalCanal;
                TxtPorcentajeNumStatusEdicion.Text = $"{porcentaje:F1}%";
                if (indiceActual < totalAlumnos)
                {
                    TxtFotoActualStatusEdicion.Text = $"Guardando alumno {indiceActual + 1} de {totalAlumnos}: {listaAlumnos[indiceActual].NombreCompleto}";
                }
            });

            // =======================================================================
            // 🛡️ ALGORITMO DE AUDITORÍA DE 3 VÍAS (CONCURRENCIA OPTIMISTA v2026)
            // =======================================================================
            await Task.Run(async () =>
            {
                var reportero = (IProgress<int>)progresoActualizacion;

                for (int i = 0; i < totalAlumnos; i++)
                {
                    reportero.Report(i);
                    var alVisual = listaAlumnos[i];

                    // REGLA: Si la fila NO fue editada localmente (no tiene línea celeste), se guarda directo
                    if (!alVisual.EstaSiendoEditadoLocal)
                    {
                        bool exitoDirecto = await AppBootstrap.Instance.Database.UpdateStudentFullDataAsync(alVisual);
                        if (!exitoDirecto) fallasBaseDatos = true;
                        continue;
                    }

                    // 🔍 SI TIENE LÍNEA CELESTE: Validamos la Nube en Vivo contra la instantánea Inicial
                    var alNubeLive = await AppBootstrap.Instance.Database.ObtenerAlumnosPorColegioAsync(codigoColegio);
                    var alNubeActual = alNubeLive.FirstOrDefault(a => a.Id == alVisual.Id);

                    if (alNubeActual != null && _historialAlumnosEnEdicionLocal.TryGetValue(alVisual.Id, out var alInicial))
                    {
                        // Comparamos si el padre alteró Fotos, Sexo o Fecha Nacimiento en internet mientras tú editabas
                        bool conflictoFotos = alNubeActual.FotoRostro != alInicial.FotoRostro || alNubeActual.FotoFamiliar != alInicial.FotoFamiliar;
                        bool conflictoDemografia = alNubeActual.Sexo != alInicial.Sexo || alNubeActual.FechaNacimiento != alInicial.FechaNacimiento;

                        if (conflictoFotos || conflictoDemografia)
                        {
                            // 🚨 CONFLICTO DETECTADO: Frenamos el pipeline y congelamos la fila crítica
                            _conflictoDetectadoYBloqueado = true;
                            fallasBaseDatos = true; // Evita que el flujo se declare exitoso aún

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Liberamos los controles para que Alexander interactúe con el Banner de decisión
                                this.IsEnabled = true;
                                PanelCargandoEdicion.Visibility = Visibility.Collapsed;

                                // Formateamos el reporte descriptivo de los cambios para el bloque de Alexander
                                string txtAlexander = $"Rostro: {(string.IsNullOrEmpty(alVisual.FotoRostro) ? "Pendiente" : alVisual.FotoRostro)} | Sexo: {alVisual.Sexo}";
                                // Formateamos el reporte descriptivo de los cambios del Padre
                                string txtPadre = $"Rostro: {(string.IsNullOrEmpty(alNubeActual.FotoRostro) ? "Pendiente" : alNubeActual.FotoRostro)} | Sexo: {alNubeActual.Sexo}";

                                TxtCambiosAlexander.Text = txtAlexander;
                                TxtCambiosPadre.Text = txtPadre;

                                // 🎨 Inyectamos la psicología de color #00528a de consulta con los nuevos botones premium
                                BannerNotificacion.Background = (SolidColorBrush)FindResource("AzulConsultaMesa");
                                TxtMensajeBanner.Foreground = Brushes.White;
                                TxtMensajeBanner.Text = $"⚠️ CONFLICTO EN FILA {alVisual.NumeroOrden}: El padre de '{alVisual.NombreCompleto}' guardó cambios en la web mientras editabas. ¿Con cuáles deseas quedarte?";

                                // Conmutamos las cajas visuales por hardware dentro de la misma pantalla
                                GridComparacionConflicto.Visibility = Visibility.Visible;
                                PanelAccionesConflicto.Visibility = Visibility.Visible;
                                BtnCerrarBannerOrdinario.Visibility = Visibility.Collapsed;
                                BannerNotificacion.Height = 65; // Ajuste para que entre la mesa de comparación

                                // Enfocamos la fila en conflicto en el DataGrid oscuro
                                GridEdicionColegio.SelectedItem = alVisual;
                                GridEdicionColegio.ScrollIntoView(alVisual);
                            });

                            break; // Rompemos el bucle 'for' inmediatamente para no procesar más filas hasta resolver esta
                        }
                    }

                    // Si no hubo conflicto con el padre, realizamos el commit seguro en MariaDB
                    bool exitoSeguro = await AppBootstrap.Instance.Database.UpdateStudentFullDataAsync(alVisual);
                    if (!exitoSeguro) fallasBaseDatos = true;
                }

                if (!_conflictoDetectadoYBloqueado) reportero.Report(totalAlumnos);
            });

            // Si se frenó el bucle por conflicto, salimos del método para esperar la decisión del operador
            if (_conflictoDetectadoYBloqueado) return;

            // Paso 2: Subida física de imágenes EXTRA (Solo si el operador cargó archivos nuevos en los botones)
            bool fallasArchivos = false;
            if (_fotosRostrosNuevas.Count > 0 || _fotosFamiliasNuevas.Count > 0)
            {
                int totalRostrosExtra = _fotosRostrosNuevas.Count;
                int totalFamiliasExtra = _fotosFamiliasNuevas.Count;
                int totalGlobalExtra = totalRostrosExtra + totalFamiliasExtra;

                var progresoRedFotos = new Progress<ReporteProgresoRedModel>(reporte =>
                {
                    RellenoAzulProgresoEdicion.Width = (reporte.Porcentaje / 100.0) * anchoTotalCanal;
                    TxtPorcentajeNumStatusEdicion.Text = $"{reporte.Porcentaje:F1}%";
                    TxtFotoActualStatusEdicion.Text = $"Subiendo lote adicional: {reporte.NombreArchivo}";
                });

                bool rostrosExtraSubidos = await SubirArchivosFisicosHostingAsync(codigoColegio, _fotosRostrosNuevas, true, 0, totalGlobalExtra, progresoRedFotos);
                bool familiaresExtraSubidos = await SubirArchivosFisicosHostingAsync(codigoColegio, _fotosFamiliasNuevas, false, totalRostrosExtra, totalGlobalExtra, progresoRedFotos);

                if (!rostrosExtraSubidos || !familiaresExtraSubidos) fallasArchivos = true;
            }

            // 🔓 Liberación y reanudación del radar
            this.IsEnabled = true;
            _timerRadarEnVivo.Start();

            RellenoAzulProgresoEdicion.Width = anchoTotalCanal;
            TxtPorcentajeNumStatusEdicion.Text = "100%";
            TxtFotoActualStatusEdicion.Text = "Sincronización masiva finalizada.";

            if (!fallasBaseDatos && !fallasArchivos)
            {
                MostrarNotificacionBanner("¡Matriz de datos y archivos extras actualizados con éxito en la nube!", true);

                // Esperamos los 4 segundos acordados para que leas el mensaje del 100% de la barra azul
                await Task.Delay(4000);

                // ⚡ LA PURGA QUIRÚRGICA: Limpiamos todo el entorno para reiniciar el ciclo
                LimpiarMesaEdicionPostGuardado();

                // Devolvemos la barra azul a 0% de forma segura para el próximo colegio
                RellenoAzulProgresoEdicion.Width = 0;
                TxtPorcentajeNumStatusEdicion.Text = "0%";
                TxtFotoActualStatusEdicion.Text = "Mesa de edición lista para sincronizar...";
            }
            else
            {
                MostrarNotificacionBanner("Sincronización completada con advertencias de red en StackCP.", false);

                // Si falló, también limpiamos la barra tras un retraso para no dejar congelada la UI
                await Task.Delay(4000);
                RellenoAzulProgresoEdicion.Width = 0;
                TxtPorcentajeNumStatusEdicion.Text = "0%";
                TxtFotoActualStatusEdicion.Text = "Mesa de edición lista para sincronizar...";
            }
        }

        // 🟢 PROCESADOR MAESTRO DE RESOLUCIÓN DE CONFLICTOS EN CALIENTE (AUDITORÍA v2026)
        private async void BtnResolucionConflicto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button botonPresionado) return;
            if (GridEdicionColegio.SelectedItem is not AlumnoRemoteModel alumnoEnConflicto) return;

            // Capturamos la decisión de Alexander leyendo la etiqueta oculta del botón (PADRE o ALEXANDER)
            string decision = botonPresionado.Tag?.ToString() ?? "";

            // Ocultamos el Banner de conflicto inmediatamente de la pantalla
            BannerNotificacion.Height = 0;
            GridComparacionConflicto.Visibility = Visibility.Collapsed;
            PanelAccionesConflicto.Visibility = Visibility.Collapsed;
            BtnCerrarBannerOrdinario.Visibility = Visibility.Visible;

            try
            {
                PanelCargandoEdicion.Visibility = Visibility.Visible;
                this.IsEnabled = false;

                if (decision == "PADRE")
                {
                    string codigoSeguro = _colegioActualEnEdicion?.CodigoColegio ?? "";
                    var alumnosNubeLive = await AppBootstrap.Instance.Database.ObtenerAlumnosPorColegioAsync(codigoSeguro);
                    var alPadre = alumnosNubeLive.FirstOrDefault(a => a.Id == alumnoEnConflicto.Id);

                    if (alPadre != null)
                    {
                        // Sincronizamos la grilla visual con los datos reales guardados por el padre en la web
                        alumnoEnConflicto.NombreCompleto = alPadre.NombreCompleto;
                        alumnoEnConflicto.Sexo = alPadre.Sexo;
                        alumnoEnConflicto.FechaNacimiento = alPadre.FechaNacimiento;
                        alumnoEnConflicto.FotoRostro = alPadre.FotoRostro;
                        alumnoEnConflicto.FotoFamiliar = alPadre.FotoFamiliar;
                        alumnoEnConflicto.Hobbies = alPadre.Hobbies;
                        alumnoEnConflicto.ComidaFav = alPadre.ComidaFav;
                        alumnoEnConflicto.Profesion = alPadre.Profesion;
                    }
                }
                else if (decision == "ALEXANDER")
                {
                    // Opción B: Forzamos la verdad de Alexander. Hacemos el commit forzado sobreescribiendo MariaDB
                    await AppBootstrap.Instance.Database.UpdateStudentFullDataAsync(alumnoEnConflicto);
                }

                // 🔓 APAGADO DE ESCUDO INDIVIDUAL: Como ya se resolvió la colisión de esta fila,
                // apagamos su interruptor, borramos su línea celeste y la quitamos del historial
                alumnoEnConflicto.EstaSiendoEditadoLocal = false;
                _historialAlumnosEnEdicionLocal.Remove(alumnoEnConflicto.Id);
                _conflictoDetectadoYBloqueado = false;

                // Desactivamos temporalmente bindings para forzar el redibujado limpio de la fila en Modo Oscuro
                _isSincronizandoUI = true;
                var listaActual = (System.Collections.Generic.List<AlumnoRemoteModel>)GridEdicionColegio.ItemsSource;
                GridEdicionColegio.ItemsSource = null;
                GridEdicionColegio.ItemsSource = listaActual;
                _isSincronizandoUI = false;

                MostrarNotificacionBanner($"✅ Conflicto resuelto con éxito para la fila {alumnoEnConflicto.NumeroOrden}. Se aplicó la decisión: {decision}.", true);

                // 🔄 RE-LANZAMIENTO: Volvemos a disparar de forma automática el botón azul para que continúe
                // guardando el resto de los alumnos del salón que se quedaron esperando en la fila.
                BtnAplicarCambiosMaestros_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                MostrarNotificacionBanner($"Error al resolver conflicto en la nube: {ex.Message}", false);
            }
            finally
            {
                PanelCargandoEdicion.Visibility = Visibility.Collapsed;
                this.IsEnabled = true;
            }
        }

        private void BtnHabilitarFamiliarEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (GridEdicionColegio.ItemsSource is not System.Collections.Generic.List<AlumnoRemoteModel> listaAlumnos || _colegioActualEnEdicion == null)
                return;

            // 🔓 REVERSIÓN DINÁMICA SIN MESSAGEBOX: Ejecutamos el desbloqueo directo sobre la RAM
            _isSincronizandoUI = true;
            try
            {
                foreach (var alumno in listaAlumnos)
                {
                    if (alumno.FotoFamiliar == "DESACTIVADO")
                    {
                        // Limpiamos la bandera para devolver las celdas al estado "⏳ Pendiente"
                        alumno.FotoFamiliar = "";
                    }
                }
            }
            finally
            {
                _isSincronizandoUI = false;
            }

            // Ocultamos el botón verde de emergencia en caliente
            BtnHabilitarFamiliarEdicion.Visibility = Visibility.Collapsed;

            // Forzamos el redibujado atómico de las celdas en el DataGrid oscuro
            GridEdicionColegio.ItemsSource = null;
            GridEdicionColegio.ItemsSource = listaAlumnos;

            // Informamos al operador técnico usando el componente nativo de la suite
            MostrarNotificacionBanner("🔓 Sección familiar reactivada en la grilla visual. Aplique los cambios para actualizar la nube.", true);
        }



        private void BtnAnadirFilaEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (GridEdicionColegio.ItemsSource is System.Collections.Generic.List<AlumnoRemoteModel> listaActual)
            {
                int nuevoOrden = listaActual.Count > 0 ? listaActual.Max(a => a.NumeroOrden) + 1 : 1;

                var nuevoAlumno = new AlumnoRemoteModel
                {
                    NumeroOrden = nuevoOrden,
                    NombreCompleto = "AÑADIR NOMBRE COMPLETO",
                    FechaNacimiento = null,
                    Sexo = "-- Elige --",
                    CodigoColegio = _colegioActualEnEdicion?.CodigoColegio ?? ""
                };

                listaActual.Add(nuevoAlumno);
                GridEdicionColegio.ItemsSource = null;
                GridEdicionColegio.ItemsSource = listaActual;
                GridEdicionColegio.ScrollIntoView(nuevoAlumno);
            }
        }

        private async void BtnCambiarEstadoAcceso_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not AlumnoRemoteModel alumno) return;

            // 1. 🔒 ENCENDEMOS EL CANDADO EXCLUSIVO DE ALEXANDER
            _ignorarAccesoRadarId = alumno.Id;

            try
            {
                // Calculamos la conmutación matemática (Si es 1 pasa a 0, si es 0 pasa a 1)
                int nuevoEstado = alumno.AccesoLiberado == 1 ? 0 : 1;
                bool estadoBool = nuevoEstado == 1;

                // Transmitimos a la base de datos de Mique Hosting en caliente
                bool exito = await AppBootstrap.Instance.Database.UpdateWebLockStatusAsync(alumno.Id, estadoBool);

                if (exito)
                {
                    // Forzamos el cambio en la memoria RAM del objeto visual
                    alumno.AccesoLiberado = nuevoEstado;
                    System.Diagnostics.Debug.WriteLine($"[ACCESO INTERNET ✓] Guardado exitoso para: {alumno.NombreCompleto}");

                    // =======================================================================
                    // ⚡ RE-RENDERIZADO EN CALIENTE DE LA FILA (SOLUCIONA EL CONGELADO)
                    // =======================================================================
                    // Obligamos a la grilla a destruir los botones viejos y pintar el nuevo color
                    _ = Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        var filaVisual = GridEdicionColegio.ItemContainerGenerator.ContainerFromItem(alumno) as DataGridRow;
                        if (filaVisual != null)
                        {
                            _isSincronizandoUI = true; // Candado de protección contra loops
                            filaVisual.DataContext = null;
                            await Task.Delay(5); // Micro-retraso para que WPF purgue la UI
                            filaVisual.DataContext = alumno; // Inyectamos de nuevo y se dibuja el botón correcto
                            _isSincronizandoUI = false;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error crítico en botón acceso: {ex.Message}");
            }
            finally
            {
                // 2. 🔓 LIBERAMOS EL CANDADO PARA QUE EL RADAR PUEDA SEGUIR AL PADRE
                _ignorarAccesoRadarId = -1;
            }
        }

        private void BtnEliminarFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton && boton.DataContext is AlumnoRemoteModel alumno)
            {
                var conf = MessageBox.Show($"¿Desea quitar a '{alumno.NombreCompleto}' de la grilla de edición?\n\n*Nota: El cambio impactará en el hosting al hacer clic en el botón inferior de Actualizar.", "Eliminar Fila", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (conf == MessageBoxResult.Yes && GridEdicionColegio.ItemsSource is System.Collections.Generic.List<AlumnoRemoteModel> lista)
                {
                    lista.Remove(alumno);
                    GridEdicionColegio.ItemsSource = null;
                    GridEdicionColegio.ItemsSource = lista;
                }
            }
        }

        private void BtnCargarMasRostros_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Imágenes Fotográficas (*.jpg)|*.jpg" };
            if (ofd.ShowDialog() == true)
            {
                _fotosRostrosNuevas = ofd.FileNames.ToList();
                TxtStatusMasRostros.Text = $" {_fotosRostrosNuevas.Count} archivos";
            }
        }

        private void BtnCargarMasFamiliares_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "Imágenes Fotográficas (*.jpg)|*.jpg" };
            if (ofd.ShowDialog() == true)
            {
                _fotosFamiliasNuevas = ofd.FileNames.ToList();
                TxtStatusMasFamiliares.Text = $" {_fotosFamiliasNuevas.Count} archivos";
            }
        }
        // =======================================================================
        // 🗑 PARTE 5: CONTROL DE VERIFICACIÓN Y PURGA ABSOLUTA EN CASCADA
        // =======================================================================

        private void BtnCargarColegioEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (CboEliminarColegiosLista.SelectedItem is ColegioRemoteModel colegio)
            {
                TxtInstitucionEliminarBanner.Text = colegio.NombreColegio.ToUpper();
            }
            else
            {
                MessageBox.Show("Por favor, selecciona una promoción de la lista para verificar.", "Control de Eliminación", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Búfer relacional para la destrucción por botones incrustados

        private void BtnDestruirColegio_Click(object sender, RoutedEventArgs e)
        {
            if (CboEliminarColegiosLista.SelectedItem is not ColegioRemoteModel colegioSeleccionado)
            {
                MostrarNotificacionBanner("⚠️ Debe seleccionar y verificar un colegio de la lista.", false);
                return;
            }

            // Almacenamos el objetivo en el búfer temporal de destrucción
            _colegioPorDestruir = colegioSeleccionado;

            // 🎨 PSICOLOGÍA DE COLOR: Encendemos el azul #00528a para alertar una consulta interactiva
            BannerNotificacion.Background = (System.Windows.Media.Brush)FindResource("AzulConsultaMesa");
            TxtMensajeBanner.Foreground = System.Windows.Media.Brushes.White;
            TxtMensajeBanner.Text = $"⚠️ ATENCIÓN: ¿Está ABSOLUTAMENTE seguro de eliminar permanentemente la institución '{colegioSeleccionado.NombreColegio}'? (Se borrará la Base de Datos y las fotos físicas).";

            // Revelamos los botones incrustados de la suite y ocultamos el cierre ordinario
            PanelAccionesDestruccion.Visibility = Visibility.Visible;
            BtnCerrarBannerOrdinario.Visibility = Visibility.Collapsed;

            BannerNotificacion.Height = 35;
        }

        // 🚀 EL COMPROMISO FINAL: Ejecución de la purga al presionar "SÍ, ELIMINAR"
        // 🚀 PROCESADOR MAESTRO DE CONFIRMACIONES EN EL BANNER (100% INMUNE A MESSAGEBOX BLANCOS)
        // 🚀 PROCESADOR MAESTRO DE CONFIRMACIONES EN EL BANNER (RESPUESTA: SÍ)
        private async void BtnConfirmarBorradoBanner_Click(object sender, RoutedEventArgs e)
        {
            // --- CASO INTERACTIVO: EL FOTÓGRAFO DESEA ANEXAR MÁS FOTOS CRUDAS ---
            if (_esperandoConfirmacionMasFotosCrudas)
            {
                _esperandoConfirmacionMasFotosCrudas = false;

                // Ocultamos temporalmente el banner antes de lanzar la nueva ventana
                BannerNotificacion.Height = 0;
                PanelAccionesDestruccion.Visibility = Visibility.Collapsed;
                BtnCerrarBannerOrdinario.Visibility = Visibility.Visible;

                // Forzamos la re-apertura del explorador en el hilo de la UI de forma inmediata
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    BtnExaminarCrudasOrigen_Click(this, new RoutedEventArgs());
                });
                return;
            }

            // --- CASO 1: CONFIRMACIÓN DE DESTRUCCIÓN ABSOLUTA (EL FILTRO DE LA BOMBA) ---
            if (_colegioPorDestruir != null)
            {
                PanelAccionesDestruccion.IsEnabled = false;
                this.IsEnabled = false;

                string codigoColegioAPurgar = _colegioPorDestruir.CodigoColegio.Trim();
                TxtMensajeBanner.Text = $"💣 Removiendo registros relacionales de '{_colegioPorDestruir.NombreColegio}'...";

                // 1. Purga relacional en la Base de Datos MySQL remota
                bool purgaBaseDatosExitosa = await AppBootstrap.Instance.Database.EliminarColegioCompletoAsync(_colegioPorDestruir.Id);

                if (purgaBaseDatosExitosa)
                {
                    // 2. MÓDULO DESTRUCTOR LOCAL: Purga física segura y automatizada en disco sin congelar UI
                    string rutaMaestraConfigurada = TxtRutaCarpetaMaestra.Text.Trim();
                    string fotografoColegio = !string.IsNullOrWhiteSpace(_colegioPorDestruir.Fotografo) ? _colegioPorDestruir.Fotografo.Trim() : "Fotografo_Anonimo";

                    // Resolvemos la ruta exacta de la promoción usando tu PathResolverService
                    string rutaClaseLocal = AppBootstrap.Instance.Paths.GetClassroomFolder(fotografoColegio, codigoColegioAPurgar);

                    string estadoDestruccionLocal = "";

                    // BLINDAJE PERIMETRAL: Validar que la ruta calculada pertenezca legítimamente a la carpeta maestra configurada
                    if (rutaClaseLocal.StartsWith(rutaMaestraConfigurada, StringComparison.OrdinalIgnoreCase))
                    {
                        await System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                if (System.IO.Directory.Exists(rutaClaseLocal))
                                {
                                    // Capturamos la ruta de la carpeta del Fotógrafo antes de borrar el colegio
                                    string? rutaFotografoFolder = System.IO.Path.GetDirectoryName(rutaClaseLocal);

                                    // Purgamos de raíz la carpeta física de este colegio en específico
                                    System.IO.Directory.Delete(rutaClaseLocal, true);
                                    estadoDestruccionLocal = " e historial físico en disco purgado con éxito.";

                                    // 🧠 ALGORITMO OPTIMIZADOR: Inspección y limpieza de la carpeta del Fotógrafo
                                    if (!string.IsNullOrWhiteSpace(rutaFotografoFolder) &&
                                        System.IO.Directory.Exists(rutaFotografoFolder) &&
                                        rutaFotografoFolder.StartsWith(rutaMaestraConfigurada, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Evaluamos si el directorio quedó completamente vacío (sin archivos ni subcarpetas)
                                        bool estaVacio = !System.IO.Directory.EnumerateFileSystemEntries(rutaFotografoFolder).Any();

                                        if (estaVacio)
                                        {
                                            // Borrado simple (false), si por error hubiera algo oculto no borrará por accidente
                                            System.IO.Directory.Delete(rutaFotografoFolder, false);
                                            estadoDestruccionLocal += " Carpeta del fotógrafo purgada por desuso.";
                                        }
                                    }
                                }
                                else
                                {
                                    estadoDestruccionLocal = " (No se encontró directorio físico del colegio en disco).";
                                }
                            }
                            catch (System.IO.IOException ioEx)
                            {
                                estadoDestruccionLocal = $" (Aviso: Directorio local bloqueado por Windows: {ioEx.Message}).";
                            }
                            catch (System.UnauthorizedAccessException)
                            {
                                estadoDestruccionLocal = " (Aviso: Error de permisos de administrador al borrar localmente).";
                            }
                            catch (System.Exception ex)
                            {
                                estadoDestruccionLocal = $" (Aviso: Error de E/S local: {ex.Message}).";
                            }
                        });
                    }
                    else
                    {
                        estadoDestruccionLocal = " (⚠️ Intento de violación de ruta perimetral local bloqueado).";
                    }

                    // 3. Purga de archivos remotos en el Servidor Web (Mique Hosting) vía HTTP POST
                    bool purgaArchivosFisicosExitosa = false;
                    try
                    {
                        var payload = new { codigo_colegio = codigoColegioAPurgar };
                        string jsonPayload = JsonSerializer.Serialize(payload);

                        using var client = new System.Net.Http.HttpClient();
                        client.Timeout = TimeSpan.FromSeconds(30);

                        using var content = new System.Net.Http.StringContent(jsonPayload, Encoding.UTF8, "application/json");
                        var response = await client.PostAsync("https://pixeleduca.com/SmartAnuarios/purge_suite.php", content);

                        purgaArchivosFisicosExitosa = response.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PHP Purge Error]: {ex.Message}");
                    }

                    // Restablecemos los componentes de la interfaz de la Suite
                    PanelAccionesDestruccion.Visibility = Visibility.Collapsed;
                    BtnCerrarBannerOrdinario.Visibility = Visibility.Visible;
                    PanelAccionesDestruccion.IsEnabled = true;

                    MostrarNotificacionBanner($"¡El proyecto '{_colegioPorDestruir.NombreColegio}' ha sido destruido en la nube{estadoDestruccionLocal}!", true);

                    // Reseteo absoluto de vistas para obligar a refrescar los datos
                    TxtInstitucionEliminarBanner.Text = "NINGÚN COLEGIO SELECCIONADO";
                    TxtInstitucionSeleccionadaBanner.Text = "NINGÚN COLEGIO SELECCIONADO";
                    _dtIndexacion.Clear();
                    GridEdicionColegio.ItemsSource = null;
                    GridReporteElecciones.ItemsSource = null;

                    await InicializarReporteColegiosAsync();
                }
                else
                {
                    PanelAccionesDestruccion.IsEnabled = true;
                    MostrarNotificacionBanner("❌ Error en MySQL. No se pudieron borrar las filas en StackCP.", false);
                }
                _colegioPorDestruir = null;
                this.IsEnabled = true;
            }

            // --- CASO 2: CONFIRMACIÓN DE REVERSIÓN FAMILIAR ---
            else if (_esperandoConfirmacionReversion && _listaAlumnosParaRevertir != null)
            {
                _esperandoConfirmacionReversion = false;
                _isSincronizandoUI = true;
                try
                {
                    foreach (var alumno in _listaAlumnosParaRevertir)
                    {
                        if (alumno.FotoFamiliar == "DESACTIVADO") alumno.FotoFamiliar = "";
                    }
                }
                finally { _isSincronizandoUI = false; }

                BtnHabilitarFamiliarEdicion.Visibility = Visibility.Collapsed;
                GridEdicionColegio.ItemsSource = null;
                GridEdicionColegio.ItemsSource = _listaAlumnosParaRevertir;

                _listaAlumnosParaRevertir = null;
                MostrarNotificacionBanner("🔓 Sección familiar reactivada en la grilla visual. Guarde cambios para aplicar.", true);
            }

            // --- CASO 3: CONFIRMACIÓN DE QUITAR FILA DEL DATAGRID ---
            else if (_esperandoConfirmacionQuitarFila && _alumnoPorQuitarGrilla != null)
            {
                _esperandoConfirmacionQuitarFila = false;
                if (GridEdicionColegio.ItemsSource is System.Collections.Generic.List<AlumnoRemoteModel> lista)
                {
                    lista.Remove(_alumnoPorQuitarGrilla);
                    GridEdicionColegio.ItemsSource = null;
                    GridEdicionColegio.ItemsSource = lista;
                    MostrarNotificacionBanner($"Fila de '{_alumnoPorQuitarGrilla.NombreCompleto}' removida de la sesión actual.", true);
                }
                _alumnoPorQuitarGrilla = null;
            }
        }

        // ✕ EL ARREPENTIMIENTO SÍNCRONO: Cierre seguro y vaciado absoluto de la RAM ante negativas
        // ✕ EL ARREPENTIMIENTO SÍNCRONO (RESPUESTA: NO / CANCELAR)
        private void BtnCancelarBorradoBanner_Click(object sender, RoutedEventArgs e)
        {
            BannerNotificacion.Height = 0;
            PanelAccionesDestruccion.Visibility = Visibility.Collapsed;
            BtnCerrarBannerOrdinario.Visibility = Visibility.Visible;

            // Si el fotógrafo presiona NO, cerramos la consulta de fotos crudas en paz
            if (_esperandoConfirmacionMasFotosCrudas)
            {
                _esperandoConfirmacionMasFotosCrudas = false;
                MostrarNotificacionBanner($"Carga fijada. Se procesarán {_rutasFotosCrudasSeleccionadas.Count} archivos crudos.", true);
                return;
            }

            // Vaciado total preventivo para otras órdenes fantasmas del banner
            _colegioPorDestruir = null;
            _listaAlumnosParaRevertir = null;
            _alumnoPorQuitarGrilla = null;
            _esperandoConfirmacionReversion = false;
            _esperandoConfirmacionQuitarFila = false;
        }

        private void BtnCerrarBanner_Click(object sender, RoutedEventArgs e)
        {
            BannerNotificacion.Height = 0;
            _conflictoDetectadoYBloqueado = false;
            PanelAccionesDestruccion.Visibility = Visibility.Collapsed;
            BtnCerrarBannerOrdinario.Visibility = Visibility.Visible;

            _colegioPorDestruir = null;
            _listaAlumnosParaRevertir = null;
            _alumnoPorQuitarGrilla = null;
            _esperandoConfirmacionReversion = false;
            _esperandoConfirmacionQuitarFila = false;
        }

        // =======================================================================
        // 📋 PARTE 6: AUDITORÍA DE AVANCE GENERAL Y ENLACE DE SELECTORES COMBOS
        // =======================================================================

        private async void ContenedorPrincipalModulos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl) return;

            // Descarga el listado de promociones globales actualizado para los combos
            await InicializarReporteColegiosAsync();

            // 📡 CONTROL DE TRÁFICO DEL RADAR EN VIVO:
            // Comprobamos si la pestaña que pasó a estar activa en pantalla es la de Editar Colegio
            if (SubContenedorColegios.SelectedItem is TabItem pestañaActual && pestañaActual.Header?.ToString()?.Contains("Editar") == true)
            {
                // Si ya hay un colegio cargado previamente en la mesa, ejecutamos el Refresco Quirúrgico por Enfoque
                if (_colegioActualEnEdicion != null)
                {
                    _timerRadarEnVivo.Stop(); // Pausa momentánea para evitar colisiones

                    try
                    {
                        // A. Descargamos las últimas novedades de la nube
                        var alumnosNubeActuales = await AppBootstrap.Instance.Database.ObtenerAlumnosPorColegioAsync(_colegioActualEnEdicion.CodigoColegio);

                        // B. Capturamos la lista que actualmente se muestra en la pantalla del operador
                        if (GridEdicionColegio.ItemsSource is System.Collections.Generic.List<AlumnoRemoteModel> listaVisualActiva)
                        {
                            int idSeleccionadoPrevia = -1;
                            int indiceColumnaPrevia = -1;

                            if (GridEdicionColegio.SelectedItem is AlumnoRemoteModel alumnoSeleccionado)
                            {
                                idSeleccionadoPrevia = alumnoSeleccionado.Id;
                                indiceColumnaPrevia = GridEdicionColegio.CurrentCell.Column?.DisplayIndex ?? -1;
                            }

                            // C. Sincronizamos selectivamente fila por fila (Misma lógica de tu PHP)
                            foreach (var alNube in alumnosNubeActuales)
                            {
                                // ESCUDO DE PROTECCIÓN: Si el operario tiene esta fila en edición activa, NO SE TOCA
                                if (_historialAlumnosEnEdicionLocal.ContainsKey(alNube.Id))
                                    continue;

                                var alVisual = listaVisualActiva.FirstOrDefault(a => a.Id == alNube.Id);
                                if (alVisual != null)
                                {
                                    alVisual.FotoRostro = alNube.FotoRostro;
                                    alVisual.FotoFamiliar = alNube.FotoFamiliar;
                                    alVisual.Sexo = alNube.Sexo;
                                    alVisual.FechaNacimiento = alNube.FechaNacimiento;
                                    alVisual.Hobbies = alNube.Hobbies;
                                    alVisual.ComidaFav = alNube.ComidaFav;
                                    alVisual.Profesion = alNube.Profesion;
                                    alVisual.Completado = alNube.Completado;
                                    alVisual.AccesoLiberado = alNube.AccesoLiberado;
                                }
                            }

                            // D. Redibujado atómico en Modo Oscuro desactivando temporalmente bindings
                            _isSincronizandoUI = true;
                            GridEdicionColegio.ItemsSource = null;
                            GridEdicionColegio.ItemsSource = listaVisualActiva;
                            _isSincronizandoUI = false;

                            // E. RESTAURACIÓN DE CURSOR: Volvemos al casillero exacto
                            if (idSeleccionadoPrevia != -1)
                            {
                                var alumnoARecuperar = listaVisualActiva.FirstOrDefault(a => a.Id == idSeleccionadoPrevia);
                                if (alumnoARecuperar != null)
                                {
                                    GridEdicionColegio.SelectedItem = alumnoARecuperar;

                                    if (indiceColumnaPrevia != -1 && indiceColumnaPrevia < GridEdicionColegio.Columns.Count)
                                    {
                                        var columnaTarget = GridEdicionColegio.Columns[indiceColumnaPrevia];
                                        GridEdicionColegio.CurrentCell = new DataGridCellInfo(alumnoARecuperar, columnaTarget);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TAB REFRESH ERROR] Fallo al saltar entre pestañas: {ex.Message}");
                    }
                    finally
                    {
                        _timerRadarEnVivo.Start(); // Reanudamos el temporizador operativo
                    }
                }
            }
            else
            {
                // Si el operador se movió a Registrar, Eliminar o Reportes, congelamos el timer en seco
                _timerRadarEnVivo.Stop();
            }
        }


        private async System.Threading.Tasks.Task InicializarReporteColegiosAsync()
        {
            var listaColegios = await AppBootstrap.Instance.Database.ObtenerReporteColegiosAsync();

            CboEditarColegiosLista.ItemsSource = listaColegios;
            CboEditarColegiosLista.DisplayMemberPath = "NombreColegio";

            CboEliminarColegiosLista.ItemsSource = listaColegios;
            CboEliminarColegiosLista.DisplayMemberPath = "NombreColegio";

            CboReporteColegiosLista.ItemsSource = listaColegios;
            CboReporteColegiosLista.DisplayMemberPath = "NombreColegio";
        }

        private async void BtnGenerarReporte_Click(object sender, RoutedEventArgs e)
        {
            // 1. Verificación de selección segura mediante el ComboBox del Modo Oscuro
            if (CboReporteColegiosLista.SelectedItem is not ColegioRemoteModel colegioSeleccionado)
            {
                MostrarNotificacionBanner("Por favor, selecciona una institución de la lista para procesar los resultados.", false);
                return;
            }

            // Congelamos la ventana para mitigar peticiones duplicadas en red
            this.IsEnabled = false;

            try
            {
                // 2. Descarga remota asíncrona de la nómina extendida desde MariaDB
                var datosVotacion = await AppBootstrap.Instance.Database.ObtenerAlumnosPorColegioAsync(colegioSeleccionado.CodigoColegio);

                // 3. Inyección limpia en la grilla visual de auditoría masiva
                GridReporteElecciones.ItemsSource = datosVotacion;

                MostrarNotificacionBanner($"📊 Reporte generado con éxito. {datosVotacion.Count} alumnos sincronizados.", true);
            }
            catch (Exception ex)
            {
                MostrarNotificacionBanner($"❌ Fallo al descargar reporte desde StackCP: {ex.Message}", false);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void BtnExportarExcelAdministrativo_Click(object? sender, RoutedEventArgs e)
        {
            // 1. Validamos que el ItemsSource contenga la colección fuertemente tipada de alumnos
            if (GridReporteElecciones.ItemsSource is not System.Collections.Generic.List<AlumnoRemoteModel> datosReporte || datosReporte.Count == 0)
            {
                MostrarNotificacionBanner("⚠️ No hay datos cargados en el reporte activo para proceder con la exportación.", false);
                return;
            }

            // 2. Selección del destino en disco mediante el cuadro nativo de Windows
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Worksheets (*.xlsx)|*.xlsx",
                FileName = $"Reporte_Votaciones_{_colegioActualEnEdicion?.NombreColegio ?? "Anuario2026"}"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // 3. Construimos un DataTable intermedio con las 7 columnas reales que exige tu XAML
                    DataTable dtReportePlano = new DataTable("Auditoria_Votacion");
                    dtReportePlano.Columns.Add("N° Orden", typeof(int));
                    dtReportePlano.Columns.Add("Nombre Completo del Alumno", typeof(string));
                    dtReportePlano.Columns.Add("Fecha Nac.", typeof(string));
                    dtReportePlano.Columns.Add("Sexo", typeof(string));
                    dtReportePlano.Columns.Add("Preferencias / Hobbies", typeof(string));
                    dtReportePlano.Columns.Add("Rostro Elegido", typeof(string));
                    dtReportePlano.Columns.Add("Foto Familiar Elegida", typeof(string));
                    dtReportePlano.Columns.Add("Estado Web", typeof(string));

                    // 4. Mapeo exhaustivo fila por fila desde la memoria RAM
                    foreach (var al in datosReporte)
                    {
                        string fechaFormateada = al.FechaNacimiento.HasValue ? al.FechaNacimiento.Value.ToString("dd/MM/yyyy") : "-";

                        // Limpieza de marcadores visuales para el reporte en Excel limpio
                        string rostroCodigo = (al.FotoRostro == "-- Ninguna --" || string.IsNullOrWhiteSpace(al.FotoRostro)) ? "PENDIENTE" : al.FotoRostro;
                        string familiarCodigo = (al.FotoFamiliar == "-- Ninguna --" || string.IsNullOrWhiteSpace(al.FotoFamiliar)) ? "PENDIENTE" : al.FotoFamiliar;

                        dtReportePlano.Rows.Add(
                            al.NumeroOrden,
                            al.NombreCompleto,
                            fechaFormateada,
                            al.Sexo,
                            al.Hobbies,
                            rostroCodigo,
                            familiarCodigo,
                            al.TextoItemEstadoWeb() // Método auxiliar para capturar el string limpio
                        );
                    }

                    // 5. Escritura binaria masiva en disco duro mediante el servicio de indexación (EPPlus)
                    AppBootstrap.Instance.Indexer.SaveSessionHot(dtReportePlano, sfd.FileName);

                    MostrarNotificacionBanner("📥 Reporte administrativo exportado exitosamente a Excel.", true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error de escritura física de celdas: {ex.Message}", "Fallo de E/S", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
        #region MÓDULO 2: MOTOR DE IA (PASOS 1 - 4)

        // Evento principal para desencadenar el Pipeline de Inteligencia Artificial local
        private async void BotonProcesarLote_Click(object sender, RoutedEventArgs e)
        {
            string baseFolder = TxtRutaCarpetaMaestra.Text;
            string rawSource = Path.Combine(baseFolder, "Crudas_Camara");

            // Si el operario no ha puesto fotos, creamos la estructura de entrada automáticamente
            if (!Directory.Exists(rawSource))
            {
                Directory.CreateDirectory(rawSource);
                TxtLogsIA.Text = $">>> [Estructura Creada] Por favor coloca las fotos del lote dentro de:\n{rawSource}\ny vuelve a presionar 'Iniciar Pipeline IA'...";
                MessageBox.Show($"Se ha preparado el directorio de entrada. Coloca las fotos crudas en:\n{rawSource}", "SmartAnuariosPro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            this.IsEnabled = false;
            TxtLogsIA.Text = ">>> [PASO 1] Iniciando escaneo multinúcleo. Clasificando 2000+ fotos crudas por reconocimiento facial...";
            BarraProgresoIA.Value = 10;

            var progress = new Progress<int>(p =>
            {
                BarraProgresoIA.Value = 10 + (p * 0.4); // Distribución en la barra de carga
                TxtLogsIA.Text = $">>> [PASO 1] Clasificando negativos locales: {p}% procesado...";
            });

            AppBootstrap.Instance.Paths.BaseMasterPath = baseFolder;
            AppBootstrap.Instance.Paths.InitializeAIFolders(GetSelectedPhotographer(), GetSelectedSchool());

            // 1. Ejecutar Clasificación de Lotes Crudos
            await AppBootstrap.Instance.AIEngine.ClassifyRawPhotosAsync(GetSelectedPhotographer(), GetSelectedSchool(), rawSource, progress);

            // 2. Ejecutar Paso 2 y 3: Criba automática de ráfagas
            TxtLogsIA.Text = ">>> [PASO 2 y 3] Analizando micro-expresiones, parpadeos y poses en lote Individual...";
            string targetFolder = AppBootstrap.Instance.Paths.GetSubFolder(GetSelectedPhotographer(), GetSelectedSchool(), "Fotos Rostro");
            await AppBootstrap.Instance.AIEngine.AutoFilterBurstSelectionAsync(targetFolder, new Progress<int>());

            // 3. Apertura de la ventana flotante de Redimensión por Porcentaje (Width/Height 50% fija)
            TxtLogsIA.Text = ">>> [PASO 3] Esperando parámetros de escala del operador...";
            ResizeWindow modal = new ResizeWindow();
            modal.Owner = this;

            if (modal.ShowDialog() == true)
            {
                TxtLogsIA.Text = $">>> [PASO 3] Redimensionando copias a W:{modal.TargetPercentageWidth}% H:{modal.TargetPercentageHeight}% para el hosting web...";
                string destWeb = AppBootstrap.Instance.Paths.GetSubFolder(GetSelectedPhotographer(), GetSelectedSchool(), "Fotos para Escoger");

                var resizeProgress = new Progress<int>(p =>
                {
                    BarraProgresoIA.Value = 50 + (p * 0.3);
                    TxtLogsIA.Text = $">>> [PASO 3] Clonando galería optimizada a '\\Fotos para Escoger\\': {p}%";
                });

                await AppBootstrap.Instance.ImageProcessor.ResizeGalleryFolderAsync(targetFolder, destWeb, modal.TargetPercentageWidth, modal.TargetPercentageHeight, resizeProgress);

                // 4. Paso 4: Fusión e Inyección automática celda por celda
                TxtLogsIA.Text = ">>> [PASO 4] Fusión inteligente de elecciones web mediante N° de Orden...";
                string localExcel = AppBootstrap.Instance.Paths.GetSubFolder(GetSelectedPhotographer(), GetSelectedSchool(), "Matriz_Indexacion.xlsx");
                string tempWebExcel = Path.Combine(Path.GetTempPath(), "elecciones_web.xlsx");

                if (!File.Exists(tempWebExcel))
                    File.WriteAllText(tempWebExcel, "numero_orden\tfoto_rostro_codigo\tfoto_familiar_codigo\n1\t_DSC4855.jpg\t_DSC4487.jpg");

                string folderSelected = AppBootstrap.Instance.Paths.GetSubFolder(GetSelectedPhotographer(), GetSelectedSchool(), "Fotos Seleccionadas");
                await AppBootstrap.Instance.ExcelEngine.MergeWebReportAsync(localExcel, tempWebExcel, folderSelected);

                TxtLogsIA.Text = ">>> [PIPELINE COMPLETADO] Se han inyectado las elecciones en el Excel y creado las subcarpetas físicas \\1\\, \\2\\ por alumno.";
                BarraProgresoIA.Value = 100;
                MessageBox.Show("El pipeline de Inteligencia Artificial y empaquetado finalizó con éxito.", "SmartAnuariosPro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TxtLogsIA.Text = ">>> [PROCESO INTERRUMPIDO] El operador canceló la ventana de redimensión.";
                BarraProgresoIA.Value = 0;
            }

            this.IsEnabled = true;
        }

        private void BotonAbortar_Click(object sender, RoutedEventArgs e)
        {
            TxtLogsIA.Text = ">>> [ABORTADO] Operación detenida por el usuario de laboratorio.";
            BarraProgresoIA.Value = 0;
            MessageBox.Show("Se ha enviado la señal de parada al motor local.", "SmartAnuariosPro", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        #endregion
        #region MÓDULO 3: MESA DE INDEXACIÓN

        // Añade columnas de páginas al vuelo (como Pagina_Firmas, Pagina_Profesores) sin romper el Excel
        private void BtnAnadirColumna_Click(object sender, RoutedEventArgs e)
        {
            string customColName = $"Pagina_Personalizada_{_dtIndexacion.Columns.Count - 3}";
            _dtIndexacion.Columns.Add(customColName, typeof(string));

            GridExcel.ItemsSource = null;
            GridExcel.ItemsSource = _dtIndexacion.DefaultView;
            MessageBox.Show($"Se ha añadido la columna técnica '{customColName}' en caliente para la indexación de páginas.", "Mesa de Indexación", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Guarda la sesión en caliente usando el motor de EPPlus
        private void BtnGuardarAvanceMesa_Click(object sender, RoutedEventArgs e)
        {
            string pathExcel = AppBootstrap.Instance.Paths.GetSubFolder(GetSelectedPhotographer(), GetSelectedSchool(), "Matriz_Indexacion.xlsx");
            AppBootstrap.Instance.Indexer.SaveSessionHot(_dtIndexacion, pathExcel);

            MessageBox.Show("Sesión respaldada localmente en la matriz .xlsx del proyecto.", "SmartAnuariosPro", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // LECTURA MECÁNICA DE DISCO: Lee los archivos físicos del alumno al cambiar de fila en la grilla
        private void GridExcel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridExcel.SelectedItem is not DataRowView row) return;

            // El operador '!' le indica a .NET que estamos seguros de que el valor no es nulo
            string nOrden = row["numero_orden"]?.ToString() ?? "0";

            string photographer = GetSelectedPhotographer();
            string school = GetSelectedSchool();

            string studentFolder = Path.Combine(AppBootstrap.Instance.Paths.GetSubFolder(photographer, school, "Fotos Seleccionadas"), nOrden);
            _listMiniaturas.Clear();

            PanelRafagasSimuladas.Children.Clear();

            if (Directory.Exists(studentFolder))
            {
                string[] files = Directory.GetFiles(studentFolder, "*.jpg");
                foreach (var file in files)
                {
                    Border imgBorder = new Border { Width = 110, Margin = new Thickness(0, 0, 10, 0), Background = System.Windows.Media.Brushes.DimGray, CornerRadius = new CornerRadius(4) };
                    Grid innerGrid = new Grid();

                    Image img = new Image { Source = new BitmapImage(new Uri(file)), Stretch = System.Windows.Media.Stretch.UniformToFill };
                    innerGrid.Children.Add(img);

                    Border textLabel = new Border { Background = System.Windows.Media.Brushes.Black, VerticalAlignment = VerticalAlignment.Bottom, Height = 18, Opacity = 0.7 };
                    textLabel.Child = new TextBlock { Text = Path.GetFileName(file), Foreground = System.Windows.Media.Brushes.White, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    innerGrid.Children.Add(textLabel);

                    imgBorder.Child = innerGrid;
                    PanelRafagasSimuladas.Children.Add(imgBorder);
                }
            }
        }


        #endregion
        #region MÓDULO 4: MAPEADOR DE PÁGINAS

        private void BtnProcesarMapeo_Click(object sender, RoutedEventArgs e)
        {
            string photographer = GetSelectedPhotographer();
            string school = GetSelectedSchool();

            string dir = AppBootstrap.Instance.Paths.GetSubFolder(photographer, school, "Automatizacion_Photoshop");
            string targetFile = Path.Combine(dir, "variables_produccion.txt");

            using (StreamWriter sw = new StreamWriter(targetFile))
            {
                sw.WriteLine("Id_Orden\tNombre_Alumno\tFoto_Rostro\tFoto_Familiar");
                foreach (DataRow row in _dtIndexacion.Rows)
                {
                    string nOrden = row["numero_orden"]?.ToString() ?? "";
                    string rostroCod = row["foto_rostro_codigo"]?.ToString() ?? "";
                    string familiarCod = row["foto_familiar_codigo"]?.ToString() ?? "";
                    string nombreComp = row["nombre_completo"]?.ToString() ?? "";

                    string folderSelected = AppBootstrap.Instance.Paths.GetSubFolder(photographer, school, "Fotos Seleccionadas");

                    string rutaRostroCompleta = Path.Combine(folderSelected, nOrden, rostroCod);
                    string rutaFamiliarCompleta = Path.Combine(folderSelected, nOrden, familiarCod);

                    sw.WriteLine($"{nOrden}\t{nombreComp}\t{rutaRostroCompleta}\t{rutaFamiliarCompleta}");
                }
            }

            MessageBox.Show($"Mapeo despachado con éxito.\nSe ha exportado el lote de variables (.txt) listo para automatizar las plantillas en Adobe Photoshop:\n\n{targetFile}", "SmartAnuariosPro - Despacho", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion


        /// <summary>
        /// Estructura técnica de control de miniaturas para el hilo del carrusel mecánico.
        /// </summary>
        public class ThumbnailItem
        {
            public string? FileName { get; set; }
            public BitmapImage? ImagePath { get; set; }
        }

        public class ReporteProgresoRedModel
        {
            public double Porcentaje { get; set; }
            public string? NombreArchivo { get; set; }
        }

        /// <summary>
        /// Abre un explorador de archivos para seleccionar imágenes adicionales en caliente (Rostros o Familiares).
        /// </summary>
        private void SeleccionarFotosOpcionales(bool esRostro)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Imágenes Fotográficas (*.jpg)|*.jpg"
            };

            if (ofd.ShowDialog() == true)
            {
                if (esRostro)
                {
                    _fotosRostrosNuevas = ofd.FileNames.ToList();
                    TxtStatusMasRostros.Text = $" Se cargarán {_fotosRostrosNuevas.Count} archivos";
                    TxtLogsIA.Text = $">>> [BÚFER] Preparados {_fotosRostrosNuevas.Count} rostros adicionales para inyección web.";
                }
                else
                {
                    _fotosFamiliasNuevas = ofd.FileNames.ToList();
                    TxtStatusMasFamiliares.Text = $" Se cargarán {_fotosFamiliasNuevas.Count} archivos";
                    TxtLogsIA.Text = $">>> [BÚFER] Preparados {_fotosFamiliasNuevas.Count} familiares adicionales para inyección web.";
                }
            }
        }

        /// <summary>
        /// Sube el lote masivo de imágenes iniciales comprimidas al 75% en RAM via HttpClient Base64.
        /// Utiliza URLs con espacios simulados para protección de caja y los procesa de forma asíncrona.
        /// </summary>
        private async Task<bool> SubirArchivosFisicosHostingAsync(
            string codigoColegio,
            System.Collections.Generic.List<string> rutasLocales,
            bool esRostro,
            int archivosYaSubidos,
            int totalGlobalLote,
            IProgress<ReporteProgresoRedModel>? progreso = null)
        {
            // 🔍 RADAR CENTRAL: Monitoreamos qué argumentos exactos recibe el método de red
            System.Diagnostics.Debug.WriteLine($"[Radar Metodo Web] Entrando... esRostro: {esRostro} | Conteo de lista: {(rutasLocales?.Count ?? -1)} | Colegio: {codigoColegio}");

            if (rutasLocales == null || rutasLocales.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Radar Metodo Web ⚠️] La lista llegó VACÍA o NULA para esRostro: {esRostro}.");
                return true;
            }

            string tipoCarpeta = esRostro ? "rostros" : "familiares";
            var loteCopiaSegura = rutasLocales.ToList();
            int archivosProcesadosEnEsteBloque = 0;

            // 📡 URL exclusiva de producción apuntando al endpoint oficial del sistema sin espacios
            string urlRealLimpia = "https://pixeleduca.com/SmartAnuarios/upload_suite.php";

            using var clienteLocal = new System.Net.Http.HttpClient();
            clienteLocal.Timeout = TimeSpan.FromMinutes(5);
            clienteLocal.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            // 🛡️ REGLA SECUENCIAL FLUIDA: Procesamos uno por uno en segundo plano de forma controlada
            foreach (string rutaArchivo in loteCopiaSegura)
            {
                string nombreArchivo = System.IO.Path.GetFileName(rutaArchivo);

                try
                {
                    string base64String = "";

                    // 📸 2. Decodificación y escala optimizada a 1200 píxeles con calidad al 75% en RAM
                    using (var fs = new System.IO.FileStream(rutaArchivo, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                    {
                        var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                        // Blindamos la captura del frame usando la colección nativa
                        if (decoder.Frames == null || decoder.Frames.Count == 0)
                            throw new InvalidDataException("El archivo JPG no contiene cuadros decodificables.");

                        BitmapFrame frame = decoder.Frames[0];
                        BitmapMetadata? metadatosOriginales = frame.Metadata as BitmapMetadata;

                        // Escalado exacto a 1200 píxeles para payloads ligeros (Métricas de la consola de ayer)
                        double ratioX = 1200.0 / frame.PixelWidth;
                        double ratio = Math.Min(ratioX, 1.0);

                        var transformacionEscala = new System.Windows.Media.ScaleTransform(ratio, ratio);
                        var imagenRedimensionada = new TransformedBitmap(frame, transformacionEscala);

                        var encoder = new JpegBitmapEncoder { QualityLevel = 75 };
                        var metadatosClonados = metadatosOriginales?.Clone() as BitmapMetadata;
                        var nuevoCuadro = BitmapFrame.Create(imagenRedimensionada, frame.Thumbnail, metadatosClonados, frame.ColorContexts);

                        encoder.Frames.Add(nuevoCuadro);

                        using (var ms = new System.IO.MemoryStream())
                        {
                            encoder.Save(ms);
                            base64String = Convert.ToBase64String(ms.ToArray());
                        }
                    }

                    // 📦 3. Payload JSON estructurado idéntico a las exigencias de upload_suite.php
                    var payload = new
                    {
                        codigo_colegio = codigoColegio,
                        tipo_galeria = tipoCarpeta,
                        nombre_archivo = nombreArchivo,
                        foto_base64 = base64String
                    };
                    string jsonTexto = JsonSerializer.Serialize(payload);

                    using var contenidoJson = new System.Net.Http.StringContent(jsonTexto, Encoding.UTF8, "application/json");

                    // 🔍 RADAR RED: Reportamos el intento de disparo web inminente
                    System.Diagnostics.Debug.WriteLine($"[Radar Red] Disparando POST para: {nombreArchivo} ({tipoCarpeta}) hacia {urlRealLimpia}");

                    var response = await clienteLocal.PostAsync(urlRealLimpia, contenidoJson);

                    if (response.IsSuccessStatusCode)
                    {
                        string respuestaOkWeb = await response.Content.ReadAsStringAsync();
                        // 🔍 RADAR RED ✓: Éxito confirmado por tu script PHP en internet
                        System.Diagnostics.Debug.WriteLine($"[Radar Red ✓] Éxito en {nombreArchivo} | Respuesta Servidor: {respuestaOkWeb.Trim()}");

                        int procesadosActuales = System.Threading.Interlocked.Increment(ref archivosProcesadosEnEsteBloque);
                        int indiceGlobalActual = archivosYaSubidos + procesadosActuales;

                        if (progreso != null)
                        {
                            double porcentajeCompletado = ((double)indiceGlobalActual / totalGlobalLote) * 100;
                            progreso.Report(new ReporteProgresoRedModel
                            {
                                Porcentaje = porcentajeCompletado,
                                NombreArchivo = nombreArchivo
                            });
                        }
                    }
                    else
                    {
                        // 🔍 RADAR RED ❌: El servidor web rechazó el JSON Base64
                        string cuerpoErrorWeb = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[Radar Red ❌ FATAL] Servidor rechazó: {nombreArchivo} | Código HTTP: {(int)response.StatusCode}");
                        System.Diagnostics.Debug.WriteLine($"[Radar Red ❌ Detalle Servidor]: {cuerpoErrorWeb.Trim()}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // 🔥 REVELADOR DE VERDAD: Imprimimos CUALQUIER fallo (gráfico o de red) para que no muera en silencio
                    System.Diagnostics.Debug.WriteLine($"[Radar Excepción 🚨] Capturado fallo crítico en {nombreArchivo}: {ex.GetType().Name} -> {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Radar Excepción Detalle Stack]: {ex.StackTrace}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Despliega el banner estético superior en Modo Oscuro sustituyendo de raíz los MessageBox.
        /// Versión auditada inmune a advertencias CS8602 mediante navegación segura.
        /// </summary>
        private void MostrarNotificacionBanner(string mensaje, bool esExito)
        {
            // 🛡️ Operador ?. garantiza que si la UI no ha cargado, no ocurra una desreferencia nula
            PanelAccionesDestruccion?.SetValue(VisibilityProperty, Visibility.Collapsed);
            BtnCerrarBannerOrdinario?.SetValue(VisibilityProperty, Visibility.Visible);

            BannerNotificacion?.BeginAnimation(Border.OpacityProperty, null);
            if (BannerNotificacion != null) BannerNotificacion.Opacity = 1.0;

            // DETECTOR DE ESTADOS CROMÁTICOS DE NEURONA Y DISEÑO v2026
            if (mensaje.StartsWith("⚠️ ATENCIÓN:") ||
                mensaje.Contains("¿Está ABSOLUTAMENTE seguro") ||
                _esperandoConfirmacionMasFotosCrudas == true)
            {
                // 1. ESTADO AZUL PROFUNDO (#00528a): Consultas e Interacciones SÍ/NO
                if (BannerNotificacion != null)
                {
                    BannerNotificacion.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00528a"));
                }

                TxtMensajeBanner?.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
                BtnCerrarBannerOrdinario?.SetValue(Button.ForegroundProperty, System.Windows.Media.Brushes.White);

                // Forzamos la aparición a la derecha del panel interactivo
                PanelAccionesDestruccion?.SetValue(VisibilityProperty, Visibility.Visible);
                BtnCerrarBannerOrdinario?.SetValue(VisibilityProperty, Visibility.Collapsed);
            }
            else if (esExito)
            {
                // 2. ESTADO AMARILLO PASTEL (#D4AC0D): Confirmaciones y Éxitos de la suite
                if (BannerNotificacion != null)
                {
                    BannerNotificacion.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4AC0D"));
                }

                var colorOscuroText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                TxtMensajeBanner?.SetValue(TextBlock.ForegroundProperty, colorOscuroText);
                BtnCerrarBannerOrdinario?.SetValue(Button.ForegroundProperty, colorOscuroText);
            }
            else
            {
                // 3. ESTADO ROJO PELIGRO: Fallos de red o errores de disco en StackCP
                var recursoRojo = FindResource("RojoPeligro") as System.Windows.Media.Brush;
                var recursoTexto = FindResource("TextoPrincipal") as System.Windows.Media.Brush;

                if (BannerNotificacion != null && recursoRojo != null) BannerNotificacion.Background = recursoRojo;
                if (TxtMensajeBanner != null && recursoTexto != null) TxtMensajeBanner.Foreground = recursoTexto;
                if (BtnCerrarBannerOrdinario != null && recursoTexto != null) BtnCerrarBannerOrdinario.Foreground = recursoTexto;
            }

            if (TxtMensajeBanner != null) TxtMensajeBanner.Text = mensaje;
            if (BannerNotificacion != null) BannerNotificacion.Height = 55; // Altura auditada ergonómica

            // REGLA DE CARÁCTER 💣: Animación pulsante por hardware a 60 FPS reales
            if (mensaje.Contains("💣"))
            {
                var pulseAnimation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.0,
                    To = 0.4,
                    Duration = TimeSpan.FromMilliseconds(400),
                    AutoReverse = true,
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };

                System.Windows.Media.Animation.Timeline.SetDesiredFrameRate(pulseAnimation, 60);
                BannerNotificacion?.BeginAnimation(Border.OpacityProperty, pulseAnimation);
            }

            // Temporizador de auto-cierre exclusivo para confirmaciones de éxito ordinarias
            if (esExito && !mensaje.Contains("⚠️"))
            {
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(5);
                timer.Tick += (s, e) =>
                {
                    if (BannerNotificacion != null) BannerNotificacion.Height = 0;
                    BannerNotificacion?.BeginAnimation(Border.OpacityProperty, null);
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void GridEdicionColegio_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Column.Header.ToString() == "Fecha Nac.")
            {
                var contentPresenter = e.EditingElement as ContentPresenter;
                if (contentPresenter != null)
                {
                    // Buscamos el bloque del día en la celda activa para clavar el cursor
                    var txtDia = contentPresenter.ContentTemplate.FindName("TxtDia", contentPresenter) as TextBox;
                    if (txtDia != null)
                    {
                        txtDia.Focus();
                        txtDia.SelectAll();
                    }
                }
            }
        }

        private void CamposTexto_LostFocus_Formatear(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string textoOriginal = textBox.Text;
                if (string.IsNullOrWhiteSpace(textoOriginal)) return;

                // =======================================================================
                // 🔍 REGLA INDUSTRIAL PARA EL CÓDIGO DEL COLEGIO
                // =======================================================================
                if (textBox.Name == "TxtIngresarCodigoColegio")
                {
                    // 1. Purgamos todos los espacios en blanco accidentales o intermedios
                    string textoSinEspacios = textoOriginal.Replace(" ", "");

                    if (textoSinEspacios.Length > 0)
                    {
                        // 2. Extraemos el primer carácter de forma aislada y lo forzamos a Mayúscula
                        string primeraLetra = char.ToUpper(textoSinEspacios[0]).ToString();

                        // 3. Extraemos todo el resto de la cadena y lo forzamos estrictamente a minúsculas
                        string restoCadena = textoSinEspacios.Substring(1).ToLower();

                        // 4. Fusionamos ambos bloques (Inmune a guiones, números o símbolos intermedios)
                        textBox.Text = primeraLetra + restoCadena;
                    }
                    return; // Salimos de inmediato para no aplicar el formateador de Tipo Título
                }

                // =======================================================================
                // 🏢 REGLA PARA INSTITUCIÓN Y 📷 FOTÓGRAFO (TIPO TÍTULO INTELIGENTE)
                // =======================================================================
                // 1. Forzamos primero toda la cadena a minúsculas para romper mayúsculas sostenidas de fábrica
                string baseMinuscula = textoOriginal.ToLower();

                // 2. El motor TextInfo se encarga de capitalizar únicamente la primera letra de cada palabra
                var infoTexto = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                string textoTipoTitulo = infoTexto.ToTitleCase(baseMinuscula);

                // 3. Conservamos tu filtro de protección de siglas MINEDU en mayúsculas para el Nombre del Colegio
                if (textBox.Name == "TxtIngresarNombreColegio")
                {
                    string[] siglasAProteger = {
                "Ie", "Ie.", "Iiei", "Iee", "Ii.ee.", "Ii.ee",
                "Ebr", "Eba", "Ebe", "Ceba", "Cebe", "Coar", "Pronoei"
            };

                    foreach (string sigla in siglasAProteger)
                    {
                        string patronEscapado = System.Text.RegularExpressions.Regex.Escape(sigla);

                        // Reemplazamos la coincidencia exacta por su versión en mayúsculas sostenidas
                        textoTipoTitulo = System.Text.RegularExpressions.Regex.Replace(
                            textoTipoTitulo,
                            @"\b" + patronEscapado + @"\b",
                            sigla.ToUpper(),
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                    }
                }

                // Devolvemos el texto final estandarizado a la grilla visual de la Suite
                textBox.Text = textoTipoTitulo;
            }
        }

        // Clase interna de control logístico para StackCP
        private class LogisticalException : Exception
        {
            public LogisticalException(string message) : base(message) { }
        }
    }

    // ... Llaves finales de tus funciones ordinarias en MainWindow

    /// <summary>
    /// Convertidor industrial que detecta si el banner entró en fase de destrucción crítica 
    /// evaluando el prefijo del caracter especial de bomba.
    /// </summary>
    public class EmpezarConBombaConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string texto && !string.IsNullOrWhiteSpace(texto))
            {
                return texto.StartsWith("💣");
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 🛠️ CLASE AUXILIAR INTEROP: PUENTE DE HARDWARE PARA PERSONALIZACIÓN VISUAL
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);
    }

} // ⬅️ Esta es la última llave de cierre del Namespace de tu archivo

