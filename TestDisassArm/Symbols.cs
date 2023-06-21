using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestDisass
{
    public class Symbols : ISymbols2 
    {
        public record Symbol : ISymbol2
        {
            public SymbolType SymbolType { get; init; }

            public string Name { get; init; }

            public DisassAddressBase Address { get; init; }
        }

        Dictionary<string, Symbol> dic = new Dictionary<string, Symbol>();

        public ISymbol2 Add(string name, DisassAddressBase addr, SymbolType type)
        {
            var s = new Symbol { Name = name, Address = addr, SymbolType = type };
            dic[name] = s;
            return s;
        }


        public bool FindByName(string name, out ISymbol2 sym)
        {
            Symbol s;
            bool ret = dic.TryGetValue(name, out s);
            sym = s;
            return ret;
        }

        public IEnumerable<ISymbol2> GetByAddress(DisassAddressBase addr, SymbolType type = SymbolType.ANY)
        {
            return dic.Values.Where(v => v.Address == addr && (v.SymbolType & type) != 0);
        }

    }
}
