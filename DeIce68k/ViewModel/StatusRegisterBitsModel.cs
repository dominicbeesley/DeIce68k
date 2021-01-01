using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class StatusRegisterBitsModel : ObservableObject
    {
        bool _data = false;

        public string Label { get; init; }
        public string Name { get; init; }
        public int BitIndex { get; init; }
        public bool Data { 
            get
            {
                return _data;
            } 
            internal set
            {
                if (value != _data)
                {
                    _data = value;
                    RaisePropertyChangedEvent(nameof(Data));
                }
            }
        }

        public static StatusRegisterBitsModel TestData = new StatusRegisterBitsModel() { BitIndex = 0, Label = "XX", Name = "XXXXX", Data=true };
    }
}
