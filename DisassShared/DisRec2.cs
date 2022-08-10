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
        public IEnumerable<DisRec2OperString<Taddr>> Operands { get; init; }

        public string Hints { get; init; }

        public ushort Length { get; init; }

        public override string ToString() => $"{Mnemonic,-8} {Operands,-40}; {Hints}";

    }

    public record DisRec2OperString<Taddr>
    {
        public string Text {get; init;}
        public ISymbol2<Taddr> Symbol { get; init; }

        public override string ToString()
        {
            if (Symbol != null)
                return Symbol.Name;
            else
                return Text ?? "?";
        }
    }

}
