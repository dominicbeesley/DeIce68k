using DeIceProtocol;
using DisassShared;
using DisassX86;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class RegisterSetModelx86_386 : RegisterSetModelBase
    {
        public RegisterModel EAX { get; }
        public RegisterModel EBX { get; }
        public RegisterModel ECX { get; }
        public RegisterModel EDX { get; }
        public RegisterModel ESI { get; }
        public RegisterModel EDI { get; }
        public RegisterModel EBP { get; }
        public RegisterModel ESP { get; }
        public RegisterModel EIP { get; }

        public RegisterModel DS { get; }
        public RegisterModel ES { get; }
        public RegisterModel CS { get; }
        public RegisterModel SS { get; }

        public RegisterModel EFLAGS { get; }

        public override bool CanTrace => true;

        public override DisassAddressBase PCValue {
            //TODO: This needs a 32 bit offset for true 386
            get { return new AddressX86((UInt16)CS.Data, (UInt16)EIP.Data); }
            set {
                var x86 = value as AddressX86;
                if (x86 != null)
                {
                    EIP.Data = x86.Offset;
                    CS.Data = x86.Segment;
                }
                else
                {
                    EIP.Data = (UInt16)(value.Canonical & 0xFFFF);
                    CS.Data = (UInt16)(value.Canonical >> 16);
                }
            }
        }

        public RegisterSetModelx86_386(DeIceAppModel _parent)
        {
            Parent = _parent;

            EAX = new RegisterModel("EAX", RegisterSize.Long, 0);
            EBX = new RegisterModel("EBX", RegisterSize.Long, 0);
            ECX = new RegisterModel("ECX", RegisterSize.Long, 0);
            EDX = new RegisterModel("EDX", RegisterSize.Long, 0);
            ESI = new RegisterModel("ESI", RegisterSize.Long, 0);
            EDI = new RegisterModel("EDI", RegisterSize.Long, 0);
            EBP = new RegisterModel("EBP", RegisterSize.Long, 0);
            ESP = new RegisterModel("ESP", RegisterSize.Long, 0);
            EIP = new RegisterModel("EIP", RegisterSize.Long, 0);

            DS = new RegisterModel("DS", RegisterSize.Word, 0);
            ES = new RegisterModel("ES", RegisterSize.Word, 0);
            CS = new RegisterModel("CS", RegisterSize.Word, 0);
            SS = new RegisterModel("SS", RegisterSize.Word, 0);

            EFLAGS = new RegisterModel("EFLAGS", RegisterSize.Long, 0);

            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=31, Label="-", Name="" },
                new(this) { BitIndex=30, Label="-", Name="" },
                new(this) { BitIndex=29, Label="-", Name="" },
                new(this) { BitIndex=28, Label="-", Name="" },
                new(this) { BitIndex=27, Label="-", Name="" },
                new(this) { BitIndex=26, Label="-", Name="" },
                new(this) { BitIndex=25, Label="-", Name="" },
                new(this) { BitIndex=24, Label="-", Name="" },
                new(this) { BitIndex=23, Label="-", Name="" },
                new(this) { BitIndex=22, Label="-", Name="" },
                new(this) { BitIndex=21, Label="-", Name="" },
                new(this) { BitIndex=20, Label="-", Name="" },
                new(this) { BitIndex=19, Label="-", Name="" },
                new(this) { BitIndex=18, Label="-", Name="" },
                new(this) { BitIndex=17, Label="-", Name="" },
                new(this) { BitIndex=16, Label="v86", Name="Virtual 86" },
                new(this) { BitIndex=15, Label="R", Name="Resume Flag" },
                new(this) { BitIndex=14, Label="-", Name="" },
                new(this) { BitIndex=13, Label="NT", Name="Nested Task" },
                new(this) { BitIndex=12, Label="IOP", Name="I/O Privilege" },
                new(this) { BitIndex=11, Label="O", Name="Overflow" },
                new(this) { BitIndex=10, Label="D", Name="Direction" },
                new(this) { BitIndex=9, Label="I", Name="Interrupt Enable" },
                new(this) { BitIndex=8, Label="T", Name="Trace" },
                new(this) { BitIndex=7, Label="S", Name="Sign" },
                new(this) { BitIndex=6, Label="Z", Name="Zero" },
                new(this) { BitIndex=5, Label="-", Name="" },
                new(this) { BitIndex=4, Label="A", Name="Aux. Carry" },
                new(this) { BitIndex=3, Label="-", Name="-" },
                new(this) { BitIndex=2, Label="P", Name="Parity" },
                new(this) { BitIndex=1, Label="-", Name="-" },
                new(this) { BitIndex=0, Label="C", Name="Carry" }
            }));

            EFLAGS.PropertyChanged += FLAGS_PropertyChanged;

            foreach (var sb in StatusBits)
            {
                sb.PropertyChanged += Sb_PropertyChanged;
            }

            UpdateStatusBits();
        }

        const int DEICE_REGS_DATA_LENGTH = 0x31;

        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length < DEICE_REGS_DATA_LENGTH)
                throw new ArgumentException($"data wrong length for N_READ_RG/FN_RUN_TARG reply {nameof(RegisterSetModelx86_386)}, expecting {DEICE_REGS_DATA_LENGTH} got {deiceData.Length}");

            EDI.Data = DeIceFnFactory.ReadULong(deiceData, 0x00);
            ESI.Data = DeIceFnFactory.ReadULong(deiceData, 0x04);
            EBP.Data = DeIceFnFactory.ReadULong(deiceData, 0x08);
            EBX.Data = DeIceFnFactory.ReadULong(deiceData, 0x0C);
            EDX.Data = DeIceFnFactory.ReadULong(deiceData, 0x10);
            ECX.Data = DeIceFnFactory.ReadULong(deiceData, 0x14);
            EAX.Data = DeIceFnFactory.ReadULong(deiceData, 0x18);
            DS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x1C);
            ES.Data = DeIceFnFactory.ReadUShort(deiceData, 0x1E);
            EIP.Data = DeIceFnFactory.ReadULong(deiceData, 0x20);
            CS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x24);
            EFLAGS.Data = DeIceFnFactory.ReadULong(deiceData, 0x26);
            ESP.Data = DeIceFnFactory.ReadULong(deiceData, 0x2A);
            SS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x2E);
            TargetStatus = deiceData[0x30];
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[DEICE_REGS_DATA_LENGTH];
            DeIceFnFactory.WriteULong(ret, 0x00, EDI.Data);
            DeIceFnFactory.WriteULong(ret, 0x04, ESI.Data);
            DeIceFnFactory.WriteULong(ret, 0x08, EBP.Data);
            DeIceFnFactory.WriteULong(ret, 0x0C, EBX.Data);
            DeIceFnFactory.WriteULong(ret, 0x10, EDX.Data);
            DeIceFnFactory.WriteULong(ret, 0x14, ECX.Data);
            DeIceFnFactory.WriteULong(ret, 0x18, EAX.Data);
            DeIceFnFactory.WriteUShort(ret, 0x1C, DS.Data);
            DeIceFnFactory.WriteUShort(ret, 0x1E, ES.Data);
            DeIceFnFactory.WriteULong(ret, 0x20, EIP.Data);
            DeIceFnFactory.WriteULong(ret, 0x24, CS.Data);
            DeIceFnFactory.WriteULong(ret, 0x26, EFLAGS.Data);
            DeIceFnFactory.WriteULong(ret, 0x2A, ESP.Data);
            DeIceFnFactory.WriteUShort(ret, 0x2E, SS.Data);
            ret[0x30] = TargetStatus;
            return ret;
        }

        private void Sb_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StatusRegisterBitsModel sb = sender as StatusRegisterBitsModel;
            if (sb is not null)
            {
                uint mask = (uint)(1 << sb.BitIndex);
                EFLAGS.Data = (EFLAGS.Data & ~mask) | (sb.Data ? mask : 0);
            }
        }

        private void FLAGS_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterModel.Data))
            {
                UpdateStatusBits();
            }
        }

        private void UpdateStatusBits()
        {
            var sr = EFLAGS.Data;
            foreach (var sb in StatusBits)
            {
                sb.Data = (sr & 1 << sb.BitIndex) != 0;
            }
        }

        public override bool SetTrace(bool trace)
        {
            bool ret = (EFLAGS.Data & 0x0100) != 0;
            if (trace && (EFLAGS.Data & 0x0100) == 0)
            {
                EFLAGS.Data |= 0x0100;
            } else if (!trace && (EFLAGS.Data & 0x0100) != 0)
            {
                EFLAGS.Data &= ~(uint)0x0100;
            }

            return ret;
        }
    }
}
