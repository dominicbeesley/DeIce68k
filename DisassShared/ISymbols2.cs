﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    /// <summary>
    /// A container for symbols to be used by the disassembler
    /// </summary>
    /// <typeparam name="Taddr">The address type i.e. UInt32</typeparam>
    public interface ISymbols2<Taddr>
    {
        IEnumerable<ISymbol2<Taddr>> GetByAddress(Taddr addr);

        bool FindByName(string name, out ISymbol2<Taddr> sym);
    }
}
