using Disass68k;
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

        public ReadOnlyDictionary<uint,string> Symbols { get; }

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
                RaisePropertyChangedEvent(nameof(PC));
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

            _app.Serial.ReadMemBlock(BaseAddress + (uint)Data.Length, newData, Data.Length, (int)howmuch);

            Data = newData;

            //continue disassembly
            uint dispc = EndPoint;

            bool ok = true;
            using (var ms = new MemoryStream(Data))
            {
                ms.Position = EndPoint - BaseAddress;
                while (ok)
                {
                    string label = null;
                    if (Symbols?.TryGetValue(dispc, out label) ?? false)
                    {
                        _items.Add(new DisassItemLabelModel(_app, dispc, null, $"{label}", dispc == PC));
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    Disass.DisRec instr;
                    try
                    {

                        instr = Disass.Decode(br, dispc, Symbols, true);
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

        public DisassMemBlock(DeIceAppModel app, uint baseAddr, byte[] data, Dictionary<uint, string> symbols)
            : base()
        {
            Symbols = new ReadOnlyDictionary<uint, string>(symbols);
            _items = new ObservableCollection<DisassItemModelBase>();
            Items = new ReadOnlyObservableCollection<DisassItemModelBase>(_items);

            _app = app;
            BaseAddress = baseAddr;
            PC = baseAddr;
            Data = data;
 
            uint dispc = BaseAddress;

            //find nearest symbol
            string nearest = string.Empty;
            uint offset = uint.MaxValue;

            foreach (var x in Symbols.Keys)
            {
                if ((x <= dispc) && (dispc - x <= 0x100) & ((dispc - x) < offset))
                {
                    nearest = Symbols[x];
                    offset = dispc - x;
                }
            }

            if (nearest != string.Empty)
            {
                string o = (offset == 0) ? "" : $"+{offset:X2}";
                _items.Add(new DisassItemLabelModel(_app, dispc, null, $"{nearest}{o}", dispc == PC));
            }


            bool ok = true;
            bool first = true;
            using (var ms = new MemoryStream(Data))
            {
                while (ok)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        string label=null;
                        if (Symbols?.TryGetValue(dispc, out label) ?? false)
                        {
                            _items.Add(new DisassItemLabelModel(_app, dispc, null, $"{label}", dispc == PC));
                        }
                    }

                    var p = ms.Position;

                    var br = new BinaryReader(ms);
                    Disass.DisRec instr;
                    try
                    {

                        instr = Disass.Decode(br, dispc, Symbols, true);
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
