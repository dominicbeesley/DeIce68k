using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public abstract class DeIceFnReplyRegsBase : DeIceFnReplyBase
    {
        public byte[] RegisterData { get; }

        internal DeIceFnReplyRegsBase(byte [] regsdat) => this.RegisterData = regsdat;
    }
}
