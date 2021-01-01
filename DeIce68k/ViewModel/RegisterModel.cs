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

    public enum RegisterSize { Word, Long };

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
                return (Size == RegisterSize.Long) ? Data.ToString("X8") : Data.ToString("X4");
            }
            set
            {
                string s = value.Trim();
                try
                {
                    uint d = Convert.ToUInt32(s, 16);
                    if (Size == RegisterSize.Word)
                        d = d & 0xFFFF;
                    Data = d;
                } catch (Exception)
                {
                    Data = 0;
                }
            }
        }

        public RegisterModel(string name, RegisterSize size, UInt32 data = 0)
        {
            Name = name;
            Size = size;
            _data = data;
        }


        public static RegisterModel TestWord = new RegisterModel("XX", RegisterSize.Word, 0xBEEF);
        public static RegisterModel TestLong = new RegisterModel("XX", RegisterSize.Word, 0xDEADBEEF);
    }
}
