using DisassShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel
{
    /// <summary>
    /// Model for a dialog to get registers and address of a routine to execute on the 
    /// </summary>

    public class DeIceCallDlgModel<TRegs, TAddress> : ObservableObject where TRegs : RegisterSetModelBase where TAddress : DisassAddressBase
    {
        bool _updateMainClient = false;
        public bool UpdateMainClient { get => _updateMainClient; set => Set(ref _updateMainClient, value); }
        TAddress _address;
        public TAddress Address { get => _address; set => Set(ref _address, value); }        
        public TRegs Registers { get; init; }

        public DeIceCallDlgModel(TRegs registers, TAddress address, bool updateMainClient = false)
        {
            UpdateMainClient = updateMainClient;
            Address = address;
            Registers = registers;
        }
    }
}
