using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class DisassItemOpModel : DisassItemModelBase
    {
        public string Mnemonic { get; }
        public string Operands { get; }

        public ushort Length { get; }

        public bool Decoded { get; }

        public byte [] InstrBytes { get; }

        public string InstrBytesString {
            get
            {
                return string.Join(" ", InstrBytes.Select(x => $"{x:X2}"));
            }
        }

        private bool _isBreakpoint;
        public bool IsBreakpoint
        {
            get => _isBreakpoint;
            set => Set(ref _isBreakpoint, value);
        }

        private bool _isBreakpointEnabled;
        public bool IsBreakpointEnabled
        {
            get => _isBreakpointEnabled;
            set => Set(ref _isBreakpointEnabled, value);
        }


        public DisassItemOpModel(DeIceAppModel deIceAppModel, uint addr, string hints, byte[] instrBytes, string mnemonic, string operands, ushort length, bool decoded, bool pc)
            : base(deIceAppModel, addr, hints, pc)
        {
            Mnemonic = mnemonic;
            Operands = operands;
            Length = length;
            Decoded = decoded;
            InstrBytes = instrBytes;
            BreakpointsUpdated();
        }

        public void BreakpointsUpdated()
        {
            IsBreakpoint = Parent.Breakpoints.Where(o => Address == o.Address).Any();
            IsBreakpointEnabled = Parent.Breakpoints.Where(o => Address == o.Address && o.Enabled).Any();
        }
    }
}
