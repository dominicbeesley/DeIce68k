using DeIceProtocol;
using DisassArm;
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
    public class RegisterSetModelArm2 : RegisterSetModelBase
    {
        public RegisterModel R0 { get; }
        public RegisterModel R1 { get; }
        public RegisterModel R2 { get; }
        public RegisterModel R3 { get; }
        public RegisterModel R4 { get; }
        public RegisterModel R5 { get; }
        public RegisterModel R6 { get; }
        public RegisterModel R7 { get; }
        public RegisterModel R8 { get; }
        public RegisterModel R9 { get; }
        public RegisterModel R10 { get; }
        public RegisterModel R11 { get; }
        public RegisterModel R12 { get; }
        public RegisterModel R13_svc { get; }
        public RegisterModel R15 { get; }


        public RegisterModel R13_user { get; }
        public RegisterModel R14_user { get; }

        public RegisterModel R13_irq { get; }
        public RegisterModel R14_irq { get; }

        public RegisterModel R8_fiq { get; }
        public RegisterModel R9_fiq { get; }
        public RegisterModel R10_fiq { get; }
        public RegisterModel R11_fiq { get; }
        public RegisterModel R12_fiq { get; }
        public RegisterModel R13_fiq { get; }
        public RegisterModel R14_fiq { get; }


        //PC is not a real register but instead is the R15 value with the status bits removed
        public RegisterModel PC { get; }

        public override bool CanTrace => true;

        public override DisassAddressBase PCValue
        {
            get { return new AddressArm2(PC.Data & 0x03FFFFFF); }
            set { PC.Data = (UInt32)value.Canonical; }
        }

        public RegisterSetModelArm2(DeIceAppModel _parent)
        {
            Parent = _parent;

            R0 = new RegisterModel("R0", RegisterSize.Long, 0);
            R1 = new RegisterModel("R1", RegisterSize.Long, 0);
            R2 = new RegisterModel("R2", RegisterSize.Long, 0);
            R3 = new RegisterModel("R3", RegisterSize.Long, 0);
            R4 = new RegisterModel("R4", RegisterSize.Long, 0);
            R5 = new RegisterModel("R5", RegisterSize.Long, 0);
            R6 = new RegisterModel("R6", RegisterSize.Long, 0);
            R7 = new RegisterModel("R7", RegisterSize.Long, 0);
            R8 = new RegisterModel("R8", RegisterSize.Long, 0);
            R9 = new RegisterModel("R9", RegisterSize.Long, 0);
            R10 = new RegisterModel("R10", RegisterSize.Long, 0);
            R11 = new RegisterModel("R11", RegisterSize.Long, 0);
            R12 = new RegisterModel("R12", RegisterSize.Long, 0);
            R15 = new RegisterModel("R15", RegisterSize.Long, 0);
            R13_svc = new RegisterModel("R13s", RegisterSize.Long, 0);

            R13_user = new RegisterModel("R13u", RegisterSize.Long, 0);
            R14_user = new RegisterModel("R14u", RegisterSize.Long, 0);

            R13_irq = new RegisterModel("R13i", RegisterSize.Long, 0);
            R14_irq = new RegisterModel("R14i", RegisterSize.Long, 0);

            R8_fiq = new RegisterModel("R8f", RegisterSize.Long, 0);
            R9_fiq = new RegisterModel("R9f", RegisterSize.Long, 0);
            R10_fiq = new RegisterModel("R10f", RegisterSize.Long, 0);
            R11_fiq = new RegisterModel("R11f", RegisterSize.Long, 0);
            R12_fiq = new RegisterModel("R12f", RegisterSize.Long, 0);
            R13_fiq = new RegisterModel("R13f", RegisterSize.Long, 0);
            R14_fiq = new RegisterModel("R14f", RegisterSize.Long, 0);

            PC = new RegisterModel("PC", RegisterSize.Long, 0);


            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=31, Label="N", Name="Negative" },
                new(this) { BitIndex=30, Label="Z", Name="Zero" },
                new(this) { BitIndex=29, Label="C", Name="Carry" },
                new(this) { BitIndex=28, Label="V", Name="Overflow" },
                new(this) { BitIndex=27, Label="I", Name="IRQ" },
                new(this) { BitIndex=26, Label="F", Name="FIRQ" },
                new(this) { BitIndex=1, Label="M1", Name="M1" },
                new(this) { BitIndex=0, Label="M0", Name="M0" }
            }));

            R15.PropertyChanged += R15_PropertyChanged;
            PC.PropertyChanged += PC_PropertyChanged;

            foreach (var sb in StatusBits)
            {
                sb.PropertyChanged += Sb_PropertyChanged;
            }

            UpdateStatusBits();
        }

        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length < 0x69)
                throw new ArgumentException($"data too short FN_READ_RG/FN_RUN_TARG reply got 0x69 got 0x{deiceData.Length:X}");

            TargetStatus = deiceData[0x68];
            R0.Data = DeIceFnFactory.ReadULong(deiceData, 0x00);
            R1.Data = DeIceFnFactory.ReadULong(deiceData, 0x04);
            R2.Data = DeIceFnFactory.ReadULong(deiceData, 0x08);
            R3.Data = DeIceFnFactory.ReadULong(deiceData, 0x0C);
            R4.Data = DeIceFnFactory.ReadULong(deiceData, 0x10);
            R5.Data = DeIceFnFactory.ReadULong(deiceData, 0x14);
            R6.Data = DeIceFnFactory.ReadULong(deiceData, 0x18);
            R7.Data = DeIceFnFactory.ReadULong(deiceData, 0x1C);
            R8.Data = DeIceFnFactory.ReadULong(deiceData, 0x20);
            R9.Data = DeIceFnFactory.ReadULong(deiceData, 0x24);
            R10.Data = DeIceFnFactory.ReadULong(deiceData, 0x28);
            R11.Data = DeIceFnFactory.ReadULong(deiceData, 0x2C);
            R12.Data = DeIceFnFactory.ReadULong(deiceData, 0x30);
            R15.Data = DeIceFnFactory.ReadULong(deiceData, 0x34);
            R13_svc.Data = DeIceFnFactory.ReadULong(deiceData, 0x38);

            R13_user.Data = DeIceFnFactory.ReadULong(deiceData, 0x3C);
            R14_user.Data = DeIceFnFactory.ReadULong(deiceData, 0x40);

            R13_irq.Data = DeIceFnFactory.ReadULong(deiceData, 0x44);
            R14_irq.Data = DeIceFnFactory.ReadULong(deiceData, 0x48);

            R8_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x4C);
            R9_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x50);
            R10_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x54);
            R11_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x58);
            R12_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x5C);
            R13_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x60);
            R14_fiq.Data = DeIceFnFactory.ReadULong(deiceData, 0x64);
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[0x69];
            ret[0x68] = TargetStatus;
            DeIceFnFactory.WriteULong(ret, 0x00, R0.Data);
            DeIceFnFactory.WriteULong(ret, 0x04, R1.Data);
            DeIceFnFactory.WriteULong(ret, 0x08, R2.Data);
            DeIceFnFactory.WriteULong(ret, 0x0C, R3.Data);
            DeIceFnFactory.WriteULong(ret, 0x10, R4.Data);
            DeIceFnFactory.WriteULong(ret, 0x14, R5.Data);
            DeIceFnFactory.WriteULong(ret, 0x18, R6.Data);
            DeIceFnFactory.WriteULong(ret, 0x1C, R7.Data);
            DeIceFnFactory.WriteULong(ret, 0x20, R8.Data);
            DeIceFnFactory.WriteULong(ret, 0x24, R9.Data);
            DeIceFnFactory.WriteULong(ret, 0x28, R10.Data);
            DeIceFnFactory.WriteULong(ret, 0x2C, R11.Data);
            DeIceFnFactory.WriteULong(ret, 0x30, R12.Data);
            DeIceFnFactory.WriteULong(ret, 0x34, R15.Data);
            DeIceFnFactory.WriteULong(ret, 0x38, R13_svc.Data);

            DeIceFnFactory.WriteULong(ret, 0x3C, R13_user.Data);
            DeIceFnFactory.WriteULong(ret, 0x40, R14_user.Data);

            DeIceFnFactory.WriteULong(ret, 0x44, R13_irq.Data);
            DeIceFnFactory.WriteULong(ret, 0x48, R14_irq.Data);

            DeIceFnFactory.WriteULong(ret, 0x4C, R13_fiq.Data);
            DeIceFnFactory.WriteULong(ret, 0x50, R13_fiq.Data);
            DeIceFnFactory.WriteULong(ret, 0x54, R13_fiq.Data);
            DeIceFnFactory.WriteULong(ret, 0x58, R13_fiq.Data);
            DeIceFnFactory.WriteULong(ret, 0x5C, R13_fiq.Data);
            DeIceFnFactory.WriteULong(ret, 0x60, R13_fiq.Data);
            DeIceFnFactory.WriteULong(ret, 0x64, R14_fiq.Data);


            return ret;
        }

        private void Sb_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StatusRegisterBitsModel sb = sender as StatusRegisterBitsModel;
            if (sb is not null)
            {
                uint mask = (uint)(1 << sb.BitIndex);
                R15.Data = (R15.Data & ~mask) | ((sb.Data) ? mask : 0);
            }
        }

        private void R15_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterModel.Data))
            {
                UpdateStatusBits();
                if (PC.Data != (R15.Data & 0x03FFFFFC))
                {
                    PC.Data = R15.Data & 0x03FFFFFC;
                }
            }
        }

        private void PC_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterModel.Data))
            {
                UInt32 bd = (PC.Data & 0x03FFFFFC);
                if (PC.Data != bd)
                    PC.Data = bd;

                UInt32 nv = (R15.Data & 0xFC000003) | (PC.Data & 0x03FFFFFC);
                if (R15.Data != nv)
                    R15.Data = nv;
            }

        }

        private void UpdateStatusBits()
        {
            var sr = R15.Data;
            foreach (var sb in StatusBits)
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
