using DeIce68k.Lib;
using DisassShared;
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
        public ICommand CmdPCToHere { get; init; }
        public ICommand CmdContFromHere { get; init; }

        public DisassAddressBase Address { get; }

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


        public DisassItemModelBase(DeIceAppModel deIceAppModel, DisassAddressBase addr, string hints, bool pc)
        {
            Parent = deIceAppModel;
            CmdTraceToHere = new RelayCommand(
                (o) => { Parent.TraceTo(Address); },
                (o) => { return Parent?.Regs.IsStopped ?? false; },
                "Trace To Here",
                deIceAppModel.Command_Exception
            );
            CmdPCToHere = new RelayCommand(
                (o) => { 
                    Parent.Regs.PCValue = Address;
                    Parent.DisassMemBlock.PC = Address;
                },
                (o) => { return Parent?.Regs.IsStopped ?? false; },
                "Trace To Here",
                deIceAppModel.Command_Exception
            );
            CmdContFromHere = new RelayCommand(
                (o) => { 
                    Parent.Regs.PCValue = Address;
                    Parent.DoContinue(); },
                (o) => { return Parent?.Regs.IsStopped ?? false; },
                "Trace To Here",
                deIceAppModel.Command_Exception
            );
            Address = addr;
            Hints = hints;
            PC = pc;
        }
    }
}
