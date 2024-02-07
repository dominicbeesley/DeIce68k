using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    public interface IRegisterSetPredictNext
    {
        DisassShared.DisassAddressBase? PredictNext(byte[] programdata);
        
        int PredictProgramDataSize { get; }

    }
}
