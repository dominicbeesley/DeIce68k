using DeIce68k.ViewModel;
using DeIce68k.ViewModel.Scripts;
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
    /// Interaction logic for dlgEditCode.xaml
    /// </summary>
    public partial class dlgEditCode : Window
    {
        DeIceAppModel _app;
        public ScriptBase CompiledCode { get; protected set; }

        public dlgEditCode(DeIceAppModel app, string oldCode)
        {
            InitializeComponent();
            _app = app;
            txtCode.Text = oldCode;
            lbErrors.DataContext = new string[] { };
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> errors;
            var scriptObj = ScriptCompiler.Compile(_app, txtCode.Text, out errors);

            lbErrors.ItemsSource = errors;

            if (scriptObj != null)
            {
                CompiledCode = scriptObj;

                DialogResult = true;
                Close();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnDiscard_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            CompiledCode = null;
            Close();
        }

    }
}
