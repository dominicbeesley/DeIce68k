using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DeIce68k.ViewModel
{

    public enum RegisterSize { Word, Long, Byte, Bit, Bank24 };

    public class RegisterModel : INotifyPropertyChanged
    {
        public string Name { get; }
        public RegisterSize Size { get; }

        private UInt32 _data;

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public UInt32 Data
        {
            get
            {
                return _data;
            }
            set
            {
                if (_data != value)
                {
                    _data = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(DataString));
                }
            }
        }

        public string DataString
        {
            get
            {
                return Size switch
                {
                    RegisterSize.Long => Data.ToString("X8"),
                    RegisterSize.Bank24 => Data.ToString("X6"),
                    RegisterSize.Word => Data.ToString("X4"),
                    RegisterSize.Byte => Data.ToString("X2"),
                    RegisterSize.Bit => Data != 0 ? "1" : "0",
                    _ => Data.ToString()
                };
            }
            set
            {
                string s = value.Trim();
                try
                {
                    uint d = Convert.ToUInt32(s, 16);
                    if (Size == RegisterSize.Bank24)
                        d = d & 0xFFFFFF;
                    else if (Size == RegisterSize.Word)
                        d = d & 0xFFFF;
                    else if (Size == RegisterSize.Byte)
                        d = d & 0xFF;
                    else if (Size == RegisterSize.Bit)
                        d = d != 0 ? (uint)1 : 0;
                    Data = d;
                } catch (Exception)
                {
                    // do nothing!
                }
            }
        }

        public RegisterModel(string name, RegisterSize size, UInt32 data = 0)
        {
            Name = name;
            Size = size;
            _data = data;
        }

    }
}
