using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disass68k;
using DisassShared;

namespace DeIce68k.ViewModel
{
    public class DeIceSymbols : ISymbols2 
    {
        private DeIceAppModel _app;

        public class DeIceSymbol : ISymbol2
        {
            public SymbolType SymbolType { get; init; }

            public string Name { get; init; }

            public DisassAddressBase Address { get; init; }
        }


        ObservableCollection<DeIceSymbol> _symbolsByAddress = new ObservableCollection<DeIceSymbol>();
        public ReadOnlyObservableCollection<DeIceSymbol> SymbolsByAddress { get; init; }

        public IEnumerable<ISymbol2> All => SymbolsByAddress;

        public ISymbol2 Add(string name, DisassAddressBase address, SymbolType symboltype)
        {
            Remove(name);
            int i = 0;
            while (i < _symbolsByAddress.Count && _symbolsByAddress[i].Address < address)
                i++;

            var sym = new DeIceSymbol
            {
                Name = name,
                Address = address,
                SymbolType = symboltype

            };

            if (i >= _symbolsByAddress.Count)
            {
                _symbolsByAddress.Add(sym);
            } 
            else 
            {
                _symbolsByAddress.Insert(i, sym);
            }
            return sym;
        }

        public void Remove(string name)
        {
            foreach (var sym in _symbolsByAddress.Where(o => o.Name == name).ToList())
            {
                _symbolsByAddress.Remove(sym);
            }
        }

        public IEnumerable<ISymbol2> GetByAddress(DisassAddressBase addr, SymbolType type)
        {
            return _symbolsByAddress.Where(x =>
                (x.Address.Equals(addr))
                && (
                    (type == SymbolType.Pointer && x.SymbolType == SymbolType.NONE)
                    || (x.SymbolType & type) != 0
                    )
                    );

        }


        public bool FindByName(string name, out ISymbol2 sym)
        {
            sym = _symbolsByAddress.Where(x => x.Name == name).FirstOrDefault();
            return sym != null;
        }

        /// <summary>
        /// Finds the nearest address with defined symbol(s) _before_ the supplied address
        /// </summary>
        /// <param name="dispc">The address to find symbols before</param>
        /// <param name="found_symbols"></param>
        /// <param name="found_address"></param>
        /// <param name="limit">Symbols further than this away will be ignored - default = 256</param>
        /// <param name="symbolType">Limit to symbols matching this type or NONE</param>
        /// <returns></returns>
        public bool FindNearest(DisassAddressBase dispc, SymbolType symbolType, out IEnumerable<DeIceSymbol> found_symbols, out DisassAddressBase found_address, uint limit = 0x0100)
        {
            var sym = _symbolsByAddress
                .Select(x => new { sym = x, distance = dispc - x.Address})
                .Where(x => (x.distance >= 0) && (x.distance < limit) && (
                    (symbolType == SymbolType.Pointer && x.sym.SymbolType == SymbolType.NONE)
                    || (x.sym.SymbolType & symbolType) != 0
                    )                
                ).OrderBy(x => x.distance).Select(x => x.sym).FirstOrDefault();

            if (sym == null)
            {
                found_address = DisassAddressBase.Empty;
                found_symbols = Enumerable.Empty<DeIceSymbol>();
                return false;
            } else
            {
                found_address = sym.Address;
                found_symbols = GetByAddress(found_address, symbolType).Cast<DeIceSymbol>();
                return true;
            }

        }

        /// <summary>
        /// Find closest symbol before given address and return a string in the form sym+offset
        /// </summary>
        /// <param name="dispc">The address to match</param>
        /// <param name="limit">Limit for the search</param>
        /// <returns>Closest symbol or null if none in range</returns>
        public string FindNearest(DisassAddressBase dispc, SymbolType symbolType, uint limit = 0x100)
        {
            IEnumerable<DeIceSymbol> syms;
            DisassAddressBase near_addr;
            if (_app.Symbols.FindNearest(dispc, symbolType, out syms, out near_addr, limit))
            {
                Int64 offset = dispc - near_addr;
                string o = (offset == 0) ? "" : $"+{offset:X2}";
                return $"{syms.First().Name}{o}";
            }
            return null;
        }




        public DeIceSymbols(DeIceAppModel app)
        {
            _app = app;
            SymbolsByAddress = new ReadOnlyObservableCollection<DeIceSymbol>(_symbolsByAddress);
        }
    }
}
