using DeIceProtocol;
using Disass65816;
using DisassShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeIce68k.ViewModel
{
    public class RegisterSetModel65816 : RegisterSetModelBase
    {
        public RegisterModel A { get; }
        public RegisterModel X { get; }
        public RegisterModel Y { get; }
        public RegisterModel D { get; }
        public RegisterModel S { get; }
        public RegisterModel K { get; }
        public RegisterModel B { get; }
        public RegisterModel PC { get; }
        public RegisterModel P { get; }
        public RegisterModel E { get; }

        public override bool CanTrace => false;

        public override DisassAddressBase PCValue
        {
            get { return new Address65816(PC.Data); }
            set { PC.Data = (UInt32)value.Canonical; }
        }

        public RegisterSetModel65816(DeIceAppModel _parent)
        {
            Parent = _parent;

            A = new RegisterModel("A", RegisterSize.Word, 0);
            X = new RegisterModel("X", RegisterSize.Word, 0);
            Y = new RegisterModel("Y", RegisterSize.Word, 0);
            D = new RegisterModel("D", RegisterSize.Word, 0);
            S = new RegisterModel("S", RegisterSize.Word, 0);
            K = new RegisterModel("K", RegisterSize.Byte, 0);
            B = new RegisterModel("B", RegisterSize.Byte, 0);
            PC = new RegisterModel("PC", RegisterSize.Word, 0);
            P = new RegisterModel("P", RegisterSize.Byte, 0);
            E = new RegisterModel("E", RegisterSize.Bit, 0);

            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=8, Label="E", Name="Emulation" },
                new(this) { BitIndex=7, Label="N", Name="Negative" },
                new(this) { BitIndex=6, Label="V", Name="Overflow" },
                new(this) { BitIndex=5, Label="M", Name="Mem 8bit" },
                new(this) { BitIndex=4, Label="X", Name="IX 8bit" },
                new(this) { BitIndex=3, Label="D", Name="Decimal" },
                new(this) { BitIndex=2, Label="I", Name="Int Inh" },
                new(this) { BitIndex=1, Label="Z", Name="Zero" },
                new(this) { BitIndex=0, Label="C", Name="Carry" }
            }));

            E.PropertyChanged += E_PropertyChanged;
            P.PropertyChanged += P_PropertyChanged;

            foreach (var sb in StatusBits)
            {
                sb.PropertyChanged += Sb_PropertyChanged;
            }

            UpdateStatusBits();
        }

        const int DEICE_REGS_DATA_LENGTH = 17;

        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length < DEICE_REGS_DATA_LENGTH)
                throw new ArgumentException($"data wrong length for N_READ_RG/FN_RUN_TARG reply {nameof(RegisterSetModel68k)}, expecting {DEICE_REGS_DATA_LENGTH} got {deiceData.Length}");

            TargetStatus = deiceData[0x00];
            A.Data = DeIceFnFactory.ReadUShort(deiceData, 1);
            X.Data = DeIceFnFactory.ReadUShort(deiceData, 3);
            Y.Data = DeIceFnFactory.ReadUShort(deiceData, 5);
            D.Data = DeIceFnFactory.ReadUShort(deiceData, 7);
            S.Data = DeIceFnFactory.ReadUShort(deiceData, 9);
            B.Data = deiceData[11];
            PC.Data = DeIceFnFactory.ReadULong(deiceData, 12) & 0xFFFFFF;
            P.Data = deiceData[15];
            E.Data = (uint)(deiceData[16] & 0x01);
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[DEICE_REGS_DATA_LENGTH];
            ret[0] = TargetStatus;
            DeIceFnFactory.WriteUShort(ret, 1, A.Data);
            DeIceFnFactory.WriteUShort(ret, 3, X.Data);
            DeIceFnFactory.WriteUShort(ret, 5, Y.Data);
            DeIceFnFactory.WriteUShort(ret, 7, D.Data);
            DeIceFnFactory.WriteUShort(ret, 9, S.Data);
            ret[11] = (byte)B.Data;
            DeIceFnFactory.WriteULong(ret, 12, PC.Data);
            ret[15] = (byte)P.Data;
            ret[16] = (byte)(E.Data != 0?1:0);
            return ret;
        }

        private void Sb_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StatusRegisterBitsModel sb = sender as StatusRegisterBitsModel;
            if (sb is not null)
            {
                if (sb.BitIndex == 8)
                {
                    E.Data = sb.Data ? (uint)1 : 0;
                }
                else
                {
                    uint mask = (uint)(1 << sb.BitIndex);
                    P.Data = (P.Data & ~mask) | ((sb.Data) ? mask : 0);
                }
            }
        }

        private void E_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateStatusBits();
        }

        private void P_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateStatusBits();
        }

        private void UpdateStatusBits()
        {
            var sbE = StatusBits[0].Data = E.Data != 0 ;
            
            var sr = P.Data;
            foreach (var sb in StatusBits.Where(x => x.BitIndex <= 7))
            {
                sb.Data = (sr & 1 << sb.BitIndex) != 0;
            }
        }

        public override bool SetTrace(bool trace)
        {
            return false;
        }
    }
}
