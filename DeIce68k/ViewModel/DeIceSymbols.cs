using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disass68k;

namespace DeIce68k.ViewModel
{
    public class DeIceSymbols : IDisassSymbols
    {
        private DeIceAppModel _app;
        Dictionary<string, uint> _symbol2AddressDictionary = new Dictionary<string, uint>();
        Dictionary<uint, List<string>> _address2SymbolsdDictionary = new Dictionary<uint, List<string>>();

        public void Add(string symbol, uint address)
        {
            Remove(symbol);
            _symbol2AddressDictionary[symbol] = address;
            List<string> lst;
            if (_address2SymbolsdDictionary.TryGetValue(address, out lst))
            {
                lst.Add(symbol);
            } else
            {
                _address2SymbolsdDictionary[address] = new List<string>(new[] { symbol });
            }
        }

        public void Remove(string symbol)
        {
            uint addr;
            if (_symbol2AddressDictionary.TryGetValue(symbol, out addr))
            {
                List<string> lst;
                if (_address2SymbolsdDictionary.TryGetValue(addr, out lst))
                {
                    lst.Remove(symbol);
                }
            }
        }

        public IEnumerable<string> AddressToSymbols(uint address)
        {
            List<string> ret;
            if (_address2SymbolsdDictionary.TryGetValue(address, out ret))
            {
                return ret;
            } else
            {
                return Enumerable.Empty<string>();
            }
        }

        public bool SymbolToAddress(string symbol, out uint address)
        {            
            return _symbol2AddressDictionary.TryGetValue(symbol, out address);
        }

        /// <summary>
        /// Finds the nearest address with defined symbol(s) _before_ the supplied address
        /// </summary>
        /// <param name="dispc">The address to find symbols before</param>
        /// <param name="found_symbols"></param>
        /// <param name="found_address"></param>
        /// <param name="limit">Symbols further than this away will be ignored - default = 256</param>
        /// <returns></returns>
        public bool FindNearest(uint dispc, out IEnumerable<string> found_symbols, out uint found_address, uint limit = 0x0100)
        {
            uint offset = limit;
            bool ret = false;
            found_symbols = Enumerable.Empty<string>();
            found_address = 0;

            foreach (var x in _address2SymbolsdDictionary.Keys)
            {
                if ((x <= dispc) && ((dispc - x) < offset))
                {
                    found_symbols = _address2SymbolsdDictionary[x];
                    found_address = x;
                    offset = dispc - x;
                    ret = true;
                }
            }

            return ret;
        }

        public DeIceSymbols(DeIceAppModel app)
        {
            _app = app;
        }
    }
}
