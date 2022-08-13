using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public interface ISymbol2<Taddr>
    {
        SymbolType SymbolType { get; }
        string Name { get; }
        Taddr Address { get; }
    }
}
