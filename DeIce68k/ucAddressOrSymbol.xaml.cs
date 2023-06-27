using DisassShared;
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
    /// Interaction logic for ucAddressOrSymbol.xaml
    /// </summary>
    public partial class ucAddressOrSymbol : UserControl
    {

        public DisassAddressBase Address
        {
            get
            {
                DisassAddressBase ret;
                GetAddress(out ret);
                return ret;
            }
            set => txtAddr.Text = $"0x{value}";
        }

        public string Symbol
        {
            get => GetSymbol();
            set => txtAddr.Text = value;
        }

        public bool Valid
        {
            get
            {
                return GetAddress(out var x);
            }
        }

        public ucAddressOrSymbol()
        {
            InitializeComponent();
        }

        private void txtAddr_KeyUp(object sender, KeyEventArgs e)
        {
            var six = txtAddr.SelectionStart;
            var sl = txtAddr.SelectionLength;

            if (six + sl == txtAddr.Text.Length)
            {
                var s = txtAddr.Text.Substring(0, six);

                if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.Delete)
                {
                    txtAddr.Text = s;
                    return;
                }

                if (!string.IsNullOrEmpty(s) && !Char.IsDigit(s[0]))
                {
                    var am = (DataContext as DeIce68k.ViewModel.DeIceAppModel);
                    if (am != null)
                    {
                        var ss = am.Symbols.SymbolsByAddress.Where(x => x.Name.StartsWith(s)).FirstOrDefault()?.Name;
                        if (ss != null)
                        {
                            txtAddr.Text = ss;
                            txtAddr.SelectionStart = six;
                            txtAddr.SelectionLength = ss.Length - six;
                        }
                    }
                }
            }

        }

        protected string GetSymbol()
        {
            var s = txtAddr.Text;
            ISymbol2 sym = null;
            if ((DataContext as DeIce68k.ViewModel.DeIceAppModel)?.Symbols.FindByName(s, out sym) ?? false)
                return sym?.Name ?? null;
            else
                return null;
        }

        protected bool GetAddress(out DisassAddressBase addr)
        {
            addr = null;

            var s = txtAddr.Text.Trim();
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            var am = (DataContext as DeIce68k.ViewModel.DeIceAppModel);

            if (am != null)
            {
                ISymbol2 sym;
                if (am.Symbols.FindByName(s, out sym))
                {
                    addr = sym.Address;
                    return true;
                }
            }

            try
            {
                addr = am?.GetDisass().AddressFactory.Parse(s);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
