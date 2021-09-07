using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for Progress.xaml
    /// </summary>
    public partial class DlgProgress : Window
    {

        public event EventHandler Cancel;

        public DlgProgress()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel?.Invoke(this, EventArgs.Empty);
        }

        public double Progress
        {
            get => prgProgress.Value;
            set => prgProgress.Value = value;
        }

        public string Message
        {
            get => txtMessage.Text;
            set => txtMessage.Text = value;
        }
        
    }
}
