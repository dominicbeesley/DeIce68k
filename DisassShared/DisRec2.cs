using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public record DisRec2<Taddr>
    {
        public bool Decoded { get; init; }
        public string Mnemonic { get; init; }
        public IEnumerable<DisRec2OperString_Base> Operands { get; init; }

        public string Hints { get; init; }

        public ushort Length { get; init; }

        public override string ToString() => $"{Mnemonic,-8} {Operands,-40}; {Hints}";

    }

    public record DisRec2OperString_Base
    {

    }

    public record DisRec2OperString_String : DisRec2OperString_Base
    { 
        public string Text {get; init;}

        public override string ToString()
        {
            return Text ?? "?";
        }
        public override int GetHashCode()
        {
            return Text.GetHashCode();
        }
    }

    public record DisRec2OperString_Number : DisRec2OperString_Base
    {
        public SymbolType SymbolType { get; init; }
        public UInt32 Number { get; init; }

        public override string ToString()
        {
            return $"0x{Number:X}";
        }

        public override int GetHashCode()
        {
            return SymbolType.GetHashCode() + Number.GetHashCode();
        }
    }

}
