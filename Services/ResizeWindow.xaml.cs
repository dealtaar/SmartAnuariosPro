using System.Windows;

namespace SmartAnuariosPro.Views
{
    public partial class ResizeWindow : Window
    {
        // Propiedades para devolver los valores seleccionados por el operador a la ventana principal
        public double TargetPercentageWidth { get; private set; } = 50.0;
        public double TargetPercentageHeight { get; private set; } = 50.0;
        private bool _isSyncing = false;

        public ResizeWindow()
        {
            InitializeComponent();
        }

        // Sincroniza el ancho con el alto para preservar la relación de aspecto de forma obligatoria
        private void SldWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncing) return;
            if (ChkAspectRatio != null && ChkAspectRatio.IsChecked == true)
            {
                _isSyncing = true;
                if (SldHeight != null) SldHeight.Value = e.NewValue;
                _isSyncing = false;
            }
        }

        // Sincroniza el alto con el ancho para preservar la relación de aspecto de forma obligatoria
        private void SldHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncing) return;
            if (ChkAspectRatio != null && ChkAspectRatio.IsChecked == true)
            {
                _isSyncing = true;
                if (SldWidth != null) SldWidth.Value = e.NewValue;
                _isSyncing = false;
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Indica a la ventana padre que la operación se canceló
            this.Close();
        }

        private void BtnRedimensionar_Click(object sender, RoutedEventArgs e)
        {
            // Congelamos los porcentajes seleccionados por el operario antes de cerrar
            TargetPercentageWidth = SldWidth.Value;
            TargetPercentageHeight = SldHeight.Value;

            this.DialogResult = true; // Indica a la ventana padre que la operación fue aprobada
            this.Close();
        }
    }
}
