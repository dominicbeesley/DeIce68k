using DeIceProtocol;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DeIce68k.ViewModel
{
    public class TargetStatusByte_ValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                byte b = (byte)(value ?? 0);
                switch (b)
                {
                    case DeIceProtoConstants.TS_RUNNING:
                        return "run";
                    case DeIceProtoConstants.TS_BP:
                        return "breakpoint";
                    case DeIceProtoConstants.TS_TRACE:
                        return "trace";
                    case DeIceProtoConstants.TS_ILLEGAL:
                        return "illegal op";
                    case 0x10:
                    case 0x11:
                    case 0x12:
                    case 0x13:
                    case 0x14:
                    case 0x15:
                    case 0x16:
                        return $"irq{(b & 0x0F):X1}";
                    case 0x17:
                        return "nmi";
                    default:
                        return "unknown";

                }
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
