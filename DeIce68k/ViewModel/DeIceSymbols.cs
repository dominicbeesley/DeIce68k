using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Disass68k;

namespace DeIce68k.ViewModel
{
    public class DeIceSymbols : IDisassSymbols
    {
        private DeIceAppModel _app;

        public class Address2Symbol : ObservableObject
        {

            internal ObservableCollection<string> _symbols = new ObservableCollection<string>();
            ReadOnlyObservableCollection<string> _symbolsRO;

            public uint Address { get; init; }
            public ReadOnlyCollection<string> Symbols { get => _symbolsRO; }

            public Address2Symbol()
            {
                _symbolsRO = new ReadOnlyObservableCollection<string>(_symbols);
            }
        }


        ObservableCollection<Address2Symbol> _symbolsByAddress = new ObservableCollection<Address2Symbol>();
        ReadOnlyObservableCollection<Address2Symbol> _symbolsByAddressRO;

        public ReadOnlyObservableCollection<Address2Symbol> SymbolsByAddress { get => _symbolsByAddressRO; }

        Dictionary<string, uint> _symbol2AddressDictionary = new Dictionary<string, uint>();
        Dictionary<uint, List<string>> _address2SymbolsdDictionary = new Dictionary<uint, List<string>>();

        public ReadOnlyDictionary<string, uint> Symbol2AddressDictionary
        {
            get;           
        }

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
            int i = 0;
            while (i < _symbolsByAddress.Count && _symbolsByAddress[i].Address < address)
                i++;

            if (i >= _symbolsByAddress.Count)
            {
                var sa = new Address2Symbol() { Address = address };
                sa._symbols.Add(symbol);
                _symbolsByAddress.Add(sa);
            } 
            else if (_symbolsByAddress[i].Address == address)
            {
                var sa = _symbolsByAddress[i];
                sa._symbols.Insert(sa._symbols.Where(o => o.CompareTo(symbol) < 0).Count(), symbol);
            } else
            {
                var sa = new Address2Symbol() { Address = address };
                sa._symbols.Add(symbol);
                _symbolsByAddress.Insert(i, sa);
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

                Address2Symbol s = _symbolsByAddress.Where(s => s.Address == addr).FirstOrDefault();
                if (s != null)
                {
                    s._symbols.Remove(symbol);
                    if (s._symbols.Count == 0)
                        _symbolsByAddress.Remove(s);
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

        /// <summary>
        /// Find closest symbol before given address and return a string in the form sym+offset
        /// </summary>
        /// <param name="dispc">The address to match</param>
        /// <param name="limit">Limit for the search</param>
        /// <returns>Closest symbol or null if none in range</returns>
        public string FindNearest(uint dispc, uint limit = 0x100)
        {
            IEnumerable<string> syms;
            uint near_addr;
            if (_app.Symbols.FindNearest(dispc, out syms, out near_addr, limit))
            {
                uint offset = dispc - near_addr;
                string o = (offset == 0) ? "" : $"+{offset:X2}";
                return $"{syms.First()}{o}";
            }
            return null;
        }


    public DeIceSymbols(DeIceAppModel app)
        {
            _app = app;
            _symbolsByAddressRO = new ReadOnlyObservableCollection<Address2Symbol>(_symbolsByAddress);
            Symbol2AddressDictionary = new ReadOnlyDictionary<string, uint>(_symbol2AddressDictionary);
        }
    }
}
