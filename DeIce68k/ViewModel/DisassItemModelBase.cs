using DeIce68k.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    /// <summary>
    /// represents a line in a disassembly
    /// </summary>
    public abstract class DisassItemModelBase : ObservableObject
    {
        public DeIceAppModel Parent { get; init; }


        public ICommand CmdTraceToHere { get; init; }

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


        public DisassItemModelBase(DeIceAppModel deIceAppModel, uint addr, string hints, bool pc)
        {
            Parent = deIceAppModel;
            CmdTraceToHere = new RelayCommand<object>(
                (o) => { Parent.TraceTo(Address); },
                (o) => { return Parent.Regs.IsStopped; }
            );
            Address = addr;
            Hints = hints;
            PC = pc;
        }
    }
}
