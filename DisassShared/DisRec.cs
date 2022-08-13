using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public record DisRec
    {
        public bool Decoded { get; init; }
        public string Mnemonic { get; init; }
        public string Operands { get; init; }

        public string Hints { get; init; }

        public ushort Length { get; init; }

        public override string ToString() => $"{Mnemonic,-8} {Operands,-40}; {Hints}";

    }
}
