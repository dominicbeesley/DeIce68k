using Disass68k;
using DisassShared;
using DisassX86;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class DisassMemBlock : ObservableObject
    {
        DeIceAppModel _app;

        public uint BaseAddress { get; }
        public byte[] Data { get; protected set; }

        ObservableCollection<DisassItemModelBase> _items;
        public ReadOnlyObservableCollection<DisassItemModelBase> Items { get; }

        private uint _pc;
        public uint PC
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
                    if (i.Address == PC)
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

        public uint _endpoint;
        public uint EndPoint
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

            byte [] newData = new byte[Data.Length + howmuch];
            System.Buffer.BlockCopy(Data, 0, newData, 0, Data.Length);

            _app.DeIceProto.ReadMemBlock(BaseAddress + (uint)Data.Length, newData, Data.Length, (int)howmuch);

            Data = newData;

            //continue disassembly
            uint dispc = EndPoint;

            bool ok = true;
            using (var ms = new MemoryStream(Data))
            {
                ms.Position = EndPoint - BaseAddress;
                while (ok)
                {
                    foreach (var label in _app.Symbols.AddressToSymbols(dispc)) { 
                        _items.Add(new DisassItemLabelModel(_app, dispc, null, $"{label}", dispc == PC));
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    DisRec instr;
                    try
                    {
                        instr = Disass.Decode(br, dispc, _app.Symbols, true);
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
                        _items.Add(new DisassItemOpModel(_app, dispc, instr.Hints, inst_bytes, instr.Mnemonic, instr.Operands, instr.Length, instr.Decoded, dispc == PC));

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

        public DisassMemBlock(DeIceAppModel app, uint baseAddr, byte[] data)
            : base()
        {
            _items = new ObservableCollection<DisassItemModelBase>();
            Items = new ReadOnlyObservableCollection<DisassItemModelBase>(_items);

            _app = app;
            BaseAddress = baseAddr;
            PC = baseAddr;
            Data = data;
 
            uint dispc = BaseAddress;

            IDisAss disass = new DisassX86.DisassX86();


            bool ok = true;
            bool first = true;
            using (var ms = new MemoryStream(Data))
            {
                while (ok)
                {
                    bool hassym = false;
                    foreach (var label in _app.Symbols.AddressToSymbols(dispc))
                    {
                        _items.Add(new DisassItemLabelModel(_app, dispc, null, $"{label}", dispc == PC));
                        hassym = true;
                    }
                    if (first)
                    {
                        if (!hassym)
                        {
                            string nearsym = _app.Symbols.FindNearest(dispc);
                            if (nearsym != null)
                            {
                                _items.Add(new DisassItemLabelModel(_app, dispc, null, nearsym, dispc == PC));
                            }
                        }
                        first = false;
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    DisRec2<UInt32> instr;
                    try
                    {

                        instr = disass.Decode(br, dispc);
                    }
                    catch (EndOfStreamException)
                    {
                        ok = false;
                        continue;
                    }

                    if (instr != null)
                    {

                        ms.Position = p;

                        byte [] inst_bytes = new byte[instr.Length];
                        ms.Read(inst_bytes, 0, instr.Length);
                        _items.Add(new DisassItemOpModel(_app, dispc, instr.Hints, inst_bytes, instr.Mnemonic, $"{ ExpandSymbols(_app.Symbols, instr.Operands) }", instr.Length, instr.Decoded, dispc == PC));

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

        static string ExpandSymbols(DeIceSymbols symbols, IEnumerable<DisRec2OperString_Base> oper)
        {
            StringBuilder sb = new StringBuilder();
            if (oper != null)
            {
                foreach (var o in oper)
                {
                    if (o is DisRec2OperString_Number)
                    {
                        var n = (DisRec2OperString_Number)o;
                        var s = symbols.AddressToSymbols(n.Number).FirstOrDefault();
                        if (s != null)
                            sb.Append(s);
                        else
                            sb.Append(n.ToString());
                    }
                    else
                    {
                        sb.Append(o.ToString());
                    }
                }
            }
            return sb.ToString();
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
