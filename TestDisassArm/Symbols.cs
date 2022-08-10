using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDisassArm
{
    public class Symbols : ISymbols2<UInt32>
    {
        public bool FindByName(string name, out ISymbol2<uint> sym)
        {
            sym = null;
            return false;
        }

        public IEnumerable<ISymbol2<UInt32>> GetByAddress(UInt32 addr)
        {
            return Enumerable.Empty<ISymbol2<UInt32>>();
        }

    }
}
