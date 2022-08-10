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
        public record Symbol : ISymbol2<UInt32>
        {
            public string Name { get; init; }

            public uint Address { get; init; }
        }

        Dictionary<string, Symbol> dic = new Dictionary<string, Symbol>();

        public ISymbol2<UInt32> Add(string name, UInt32 addr)
        {
            var s = new Symbol { Name = name, Address = addr };
            dic[name] = s;
            return s;
        }


        public bool FindByName(string name, out ISymbol2<UInt32> sym)
        {
            Symbol s;
            bool ret = dic.TryGetValue(name, out s);
            sym = s;
            return ret;
        }

        public IEnumerable<ISymbol2<UInt32>> GetByAddress(UInt32 addr)
        {
            return dic.Values.Where(v => v.Address == addr);
        }

    }
}
