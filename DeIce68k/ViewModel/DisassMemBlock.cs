using Disass68k;
using DisassShared;
using DisassX86;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class DisassMemBlock : ObservableObject
    {
        DeIceAppModel _app;

        public DisassAddressBase BaseAddress { get; }
        public byte[] Data { get; protected set; }

        ObservableCollection<DisassItemModelBase> _items;
        public ReadOnlyObservableCollection<DisassItemModelBase> Items { get; }

        private DisassAddressBase _pc;
        public DisassAddressBase PC
        {
            get
            {
                return _pc;
            }
            set
            {
                _pc = value;
                foreach (var i in Items)
                {
                    if (i.Address.Equals(PC))
                    {
                        if (!i.PC)
                            i.PC = true;
                    }
                    else if (i.PC)
                        i.PC = false;
                }
                RaisePropertyChangedEvent(nameof(PC));
            }
        }

        public DisassAddressBase _endpoint;
        public DisassAddressBase EndPoint
        {
            get { return _endpoint; }
            protected set
            {
                _endpoint = value;
                RaisePropertyChangedEvent(nameof(EndPoint));
            }
        }

        public void MorePlease(uint howmuch = 128)
        {

            if (_app == null)
                return;

            byte[] newData = new byte[Data.Length + howmuch];
            System.Buffer.BlockCopy(Data, 0, newData, 0, Data.Length);

            _app.DeIceProto.ReadMemBlock((BaseAddress + Data.Length).DeIceAddress, newData, Data.Length, (int)howmuch);


            Data = newData;

            //continue disassembly
            DisassAddressBase dispc = EndPoint;

            bool ok = true;
            using (var ms = new MemoryStream(Data))
            {
                ms.Position = EndPoint - BaseAddress;
                while (ok)
                {
                    foreach (var symbol in _app.Symbols.GetByAddress(dispc, SymbolType.Pointer))
                    {
                        _items.Add(new DisassItemLabelModel(_app, dispc, null, symbol.Name, dispc.Equals(PC)));
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    DisRec2<UInt32> instr;
                    try
                    {
                        instr = disAss.Decode(br, dispc);
                    }
                    catch (EndOfStreamException)
                    {
                        ok = false;
                        continue;
                    }

                    if (instr != null)
                    {

                        ms.Position = p;

                        byte[] inst_bytes = new byte[instr.Length];
                        ms.Read(inst_bytes, 0, instr.Length);
                        _items.Add(new DisassItemOpModel(_app, dispc, instr.Hints, inst_bytes, instr.Mnemonic, ExpandSymbols(_app.Symbols, instr.Operands), instr.Length, instr.Decoded, dispc.Equals(PC)));

                        dispc += instr.Length;
                        EndPoint = dispc;
                    }
                    else
                    {
                        ok = false;
                    }
                }
            }


            RaisePropertyChangedEvent(nameof(Data));

            RaisePropertyChangedEvent(nameof(Items));
        }

        IDisAss disAss;

        public DisassMemBlock(DeIceAppModel app, DisassAddressBase baseAddr, byte[] data, IDisAss disAss, IDisassState state)
            : base()
        {
            _items = new ObservableCollection<DisassItemModelBase>();
            Items = new ReadOnlyObservableCollection<DisassItemModelBase>(_items);


            _app = app;
            BaseAddress = baseAddr;
            PC = baseAddr;
            Data = data;

            DisassAddressBase dispc = baseAddr;

            this.disAss = disAss;


            bool ok = true;
            bool first = true;
            using (var ms = new MemoryStream(Data))
            {
                while (ok)
                {
                    bool hassym = false;
                    foreach (var symbol in _app.Symbols.GetByAddress(dispc, SymbolType.Pointer))
                    {
                        _items.Add(new DisassItemLabelModel(_app, dispc, null, symbol.Name, dispc.Equals(PC)));
                        hassym = true;
                    }
                    if (first)
                    {
                        if (!hassym)
                        {
                            var nearsym = _app.Symbols.FindNearest(dispc, SymbolType.Pointer);
                            if (nearsym != null)
                            {
                                _items.Add(new DisassItemLabelModel(_app, dispc, null, nearsym, dispc.Equals(PC)));
                            }
                        }
                        first = false;
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = disAss.Decode(br, dispc, state);
                    }
                    catch (EndOfStreamException)
                    {
                        ok = false;
                        continue;
                    }

                    if (instr != null)
                    {

                        ms.Position = p;

                        byte[] inst_bytes = new byte[instr.Length];
                        ms.Read(inst_bytes, 0, instr.Length);
                        _items.Add(new DisassItemOpModel(_app, dispc, instr.Hints, inst_bytes, instr.Mnemonic, ExpandSymbols(_app.Symbols, instr.Operands), instr.Length, instr.Decoded, dispc.Equals(PC)));

                        dispc += instr.Length;
                        EndPoint = dispc;
                    }
                    else
                    {
                        ok = false;
                    }
                }
            }

        }

        public IEnumerable<DisRec2OperString_Base> ExpandSymbols(DeIceSymbols symbols, IEnumerable<DisRec2OperString_Base> oper)
        {
            if (oper != null)
            {
                foreach (var o in oper)
                {
                    if (o is DisRec2OperString_Address)
                    {
                        var n = (DisRec2OperString_Address)o;                        
                        var s = symbols.GetByAddress(n.Address, n.SymbolType).FirstOrDefault();
                        if (s != null)
                            yield return new DisRec2OperString_Symbol
                            {
                                Symbol = s
                            };
                        else
                            yield return n;
                    }
                    else
                    {
                        yield return o;
                    }
                }
            }
        }


        public void BreakpointsUpdated()
        {
            foreach (var i in Items.Where(i => i is DisassItemOpModel).Cast<DisassItemOpModel>())
            {
                i.BreakpointsUpdated();
            }
        }
    }
}
