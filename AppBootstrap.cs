using SmartAnuariosPro.Services;

namespace SmartAnuariosPro
{
    public class AppBootstrap
    {
        private static AppBootstrap? _instance;

        /// <summary>
        /// Instancia única global (Singleton) para acceder a los servicios desde cualquier parte de la app.
        /// </summary>
        public static AppBootstrap Instance => _instance ??= new AppBootstrap();

        // Registro maestro de todos los componentes del backend
        public PathResolverService Paths { get; }
        public EngineAIService AIEngine { get; }
        public ImageProcessorService ImageProcessor { get; }
        public ExcelIntegrationService ExcelEngine { get; }
        public IndexingSessionService Indexer { get; }
        public DatabaseService Database { get; } // <- REGISTRO FINAL DE MYSQL

        private AppBootstrap()
        {
            // Inicialización única y centralizada de la suite técnica
            Paths = new PathResolverService();
            AIEngine = new EngineAIService();
            ImageProcessor = new ImageProcessorService();
            ExcelEngine = new ExcelIntegrationService();
            Indexer = new IndexingSessionService();
            Database = new DatabaseService(); // <- INICIALIZACIÓN FINAL
        }
    }
}
