using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIceProtocol
{
    public abstract record DeIceFnReqBase
    {
        public abstract byte FunctionCode { get; }
    }
}
