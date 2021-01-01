using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    /// <summary>
    /// represents a line in a disassembly
    /// </summary>
    public abstract class DisassItemModelBase : ObservableObject
    {
        public uint Address { get; }

        public string Hints { get; }

        private bool _pc;
        public bool PC
        {
            get
            {
                return _pc;
            }
            set
            {
                _pc = value;
                RaisePropertyChangedEvent(nameof(PC));
            }
        }


        public DisassItemModelBase(uint addr, string hints, bool pc)
        {
            Address = addr;
            Hints = hints;
            PC = pc;
        }
    }
}
