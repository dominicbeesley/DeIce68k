using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public class DisassItemLabelModel : DisassItemModelBase
    {
        public string Symbol { get; }

        public DisassItemLabelModel(DeIceAppModel deIceAppModel, DisassAddressBase addr, string hints, string sym, bool pc)
            : base(deIceAppModel, addr, hints, pc)
        {
            Symbol = sym;

        }
    }
}
