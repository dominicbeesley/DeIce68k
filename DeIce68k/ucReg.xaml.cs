using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DeIce68k
{
    /// <summary>
    /// Interaction logic for ucReg.xaml
    /// </summary>
    public partial class ucReg : UserControl
    {
        public ucReg()
        {
            InitializeComponent();
        }

        private void txtValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BindingOperations.GetBindingExpression(txtValue, TextBox.TextProperty)?.UpdateSource();
            } else if (e.Key == Key.Escape)
            {
                BindingOperations.GetBindingExpression(txtValue, TextBox.TextProperty)?.UpdateTarget();
            }
        }
    }
}
