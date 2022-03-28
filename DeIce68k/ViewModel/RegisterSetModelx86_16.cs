using DeIceProtocol;
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
    public class RegisterSetModelx86_16 : RegisterSetModelBase
    {
        public RegisterModel AX { get; }
        public RegisterModel BX { get; }
        public RegisterModel CX { get; }
        public RegisterModel DX { get; }
        public RegisterModel SI { get; }
        public RegisterModel DI { get; }
        public RegisterModel BP { get; }
        public RegisterModel SP { get; }
        public RegisterModel IP { get; }

        public RegisterModel DS { get; }
        public RegisterModel ES { get; }
        public RegisterModel CS { get; }
        public RegisterModel SS { get; }

        public RegisterModel FLAGS { get; }

        public override bool CanTrace => true;

        public override uint PCValue => IP.Data | (uint)(CS.Data << 16);

        public RegisterSetModelx86_16(DeIceAppModel _parent)
        {
            Parent = _parent;

            AX = new RegisterModel("AX", RegisterSize.Word, 0);
            BX = new RegisterModel("BX", RegisterSize.Word, 0);
            CX = new RegisterModel("CX", RegisterSize.Word, 0);
            DX = new RegisterModel("DX", RegisterSize.Word, 0);
            SI = new RegisterModel("SI", RegisterSize.Word, 0);
            SI = new RegisterModel("DI", RegisterSize.Word, 0);
            BP = new RegisterModel("BP", RegisterSize.Word, 0);
            SP = new RegisterModel("SP", RegisterSize.Word, 0);
            IP = new RegisterModel("IP", RegisterSize.Word, 0);

            DS = new RegisterModel("DS", RegisterSize.Word, 0);
            ES = new RegisterModel("ES", RegisterSize.Word, 0);
            CS = new RegisterModel("CS", RegisterSize.Word, 0);
            SS = new RegisterModel("SS", RegisterSize.Word, 0);

            FLAGS = new RegisterModel("FLAGS", RegisterSize.Word, 0);

            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=15, Label="-", Name="" },
                new(this) { BitIndex=14, Label="-", Name="" },
                new(this) { BitIndex=13, Label="-", Name="" },
                new(this) { BitIndex=12, Label="-", Name="" },
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

            FLAGS.PropertyChanged += FLAGS_PropertyChanged;

            foreach (var sb in StatusBits)
            {
                sb.PropertyChanged += Sb_PropertyChanged;
            }

            UpdateStatusBits();
        }

        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length < 0x1F)
                throw new ArgumentException("data too short FN_READ_RG/FN_RUN_TARG reply");

            DI.Data = DeIceFnFactory.ReadUShort(deiceData, 0x00);
            SI.Data = DeIceFnFactory.ReadUShort(deiceData, 0x02);
            BP.Data = DeIceFnFactory.ReadUShort(deiceData, 0x04);
            BX.Data = DeIceFnFactory.ReadUShort(deiceData, 0x06);
            DX.Data = DeIceFnFactory.ReadUShort(deiceData, 0x08);
            CX.Data = DeIceFnFactory.ReadUShort(deiceData, 0x0A);
            AX.Data = DeIceFnFactory.ReadUShort(deiceData, 0x0C);
            DS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x0E);
            ES.Data = DeIceFnFactory.ReadUShort(deiceData, 0x10);
            IP.Data = DeIceFnFactory.ReadUShort(deiceData, 0x12);
            CS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x14);
            FLAGS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x16);
            SP.Data = DeIceFnFactory.ReadUShort(deiceData, 0x18);
            SS.Data = DeIceFnFactory.ReadUShort(deiceData, 0x1A);
            TargetStatus = deiceData[0x1E];
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[0x1F];
            DeIceFnFactory.WriteUShort(ret, 0x00, DI.Data);
            DeIceFnFactory.WriteUShort(ret, 0x02, SI.Data);
            DeIceFnFactory.WriteUShort(ret, 0x04, BP.Data);
            DeIceFnFactory.WriteUShort(ret, 0x06, BX.Data);
            DeIceFnFactory.WriteUShort(ret, 0x08, DX.Data);
            DeIceFnFactory.WriteUShort(ret, 0x0A, CX.Data);
            DeIceFnFactory.WriteUShort(ret, 0x0C, AX.Data);
            DeIceFnFactory.WriteUShort(ret, 0x0E, DS.Data);
            DeIceFnFactory.WriteUShort(ret, 0x10, ES.Data);
            DeIceFnFactory.WriteUShort(ret, 0x12, IP.Data);
            DeIceFnFactory.WriteUShort(ret, 0x14, CS.Data);
            DeIceFnFactory.WriteUShort(ret, 0x16, FLAGS.Data);
            DeIceFnFactory.WriteUShort(ret, 0x18, SP.Data);
            DeIceFnFactory.WriteUShort(ret, 0x1A, SS.Data);
            ret[0x1E] = TargetStatus;
            return ret;
        }

        private void Sb_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StatusRegisterBitsModel sb = sender as StatusRegisterBitsModel;
            if (sb is not null)
            {
                uint mask = (uint)(1 << sb.BitIndex);
                FLAGS.Data = (FLAGS.Data & ~mask) | (sb.Data ? mask : 0);
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
            var sr = FLAGS.Data;
            foreach (var sb in StatusBits)
            {
                sb.Data = (sr & 1 << sb.BitIndex) != 0;
            }
        }

        public override bool SetTrace(bool trace)
        {
            bool ret = (FLAGS.Data & 0x1000) != 0;
            if (trace && (FLAGS.Data & 0x1000) == 0)
            {
                FLAGS.Data |= 0x1000;
            } else if (!trace && (FLAGS.Data & 0x1000) != 0)
            {
                FLAGS.Data &= ~(uint)0x1000;
            }

            return ret;
        }
    }
}
