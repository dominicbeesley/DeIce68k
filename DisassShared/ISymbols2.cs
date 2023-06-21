using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    /// <summary>
    /// A container for symbols to be used by the disassembler
    /// </summary>
    /// <typeparam name="Taddr">The address type i.e. UInt32</typeparam>
    public interface ISymbols2 
    {
        ISymbol2 Add(string name, DisassAddressBase addr, SymbolType type);

        IEnumerable<ISymbol2> GetByAddress(DisassAddressBase addr, SymbolType type = SymbolType.ANY);

        bool FindByName(string name, out ISymbol2 sym);

        IEnumerable<ISymbol2> All { get; }
    }
}
