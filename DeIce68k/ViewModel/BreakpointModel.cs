using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class BreakpointModel : ObservableObject
    {
        public uint Address {
            get;
            init;
        }

        private bool _enabled;
        public bool Enabled {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        private ushort _oldOP;
        public ushort OldOP
        {
            get => _oldOP;
            set => Set(ref _oldOP, value);
        }
    }
}
