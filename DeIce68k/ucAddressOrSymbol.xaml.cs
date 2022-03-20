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

        public uint Address {
            get
            {
                uint ret = 0;
                GetAddress(out ret);
                return ret;
            }
            set => txtAddr.Text = $"0x{value:X08}";
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
                uint tmp;
                return GetAddress(out tmp);
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
                        var ss = am.Symbols.Symbol2AddressDictionary.Keys.Where(x => x.StartsWith(s)).FirstOrDefault();
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
            var am = (DataContext as DeIce68k.ViewModel.DeIceAppModel);
            if (am != null)
                return am.Symbols.Symbol2AddressDictionary.Keys.Where(x => x == s).FirstOrDefault();
            else
                return null;
        }

        protected bool GetAddress(out uint addr)
        {
            addr = 0;
            var s = txtAddr.Text;
            if (string.IsNullOrEmpty(s))
                return false;

            if (Char.IsDigit(s[0]))
            {
                try
                {
                    addr = Convert.ToUInt32(s, 16);
                    return true;
                } catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                var am = (DataContext as DeIce68k.ViewModel.DeIceAppModel);
                if (am != null)
                {
                    return am.Symbols.Symbol2AddressDictionary.TryGetValue(s, out addr);
                }
                else
                    return false;
            }

        }
    }
}
