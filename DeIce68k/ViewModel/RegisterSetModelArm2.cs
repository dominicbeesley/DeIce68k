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
        public RegisterModel R13 { get; }
        public RegisterModel R15 { get; }
        public RegisterModel PC { get; }

        public override bool CanTrace => true;

        public override uint PCValue => PC.Data;

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
            R13 = new RegisterModel("R13", RegisterSize.Long, 0);
            R15 = new RegisterModel("R15", RegisterSize.Long, 0);
            PC = new RegisterModel("PC", RegisterSize.Long, 0);


            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=31, Label="N", Name="Negative" },
                new(this) { BitIndex=30, Label="Z", Name="Zero" },
                new(this) { BitIndex=13, Label="C", Name="Carry" },
                new(this) { BitIndex=12, Label="V", Name="Overflow" },
                new(this) { BitIndex=11, Label="I", Name="IRQ" },
                new(this) { BitIndex=10, Label="F", Name="FIRQ" },
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
            if (deiceData.Length < 0x41)
                throw new ArgumentException("data too short FN_READ_RG/FN_RUN_TARG reply");

            TargetStatus = deiceData[0x40];
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
            R13.Data = DeIceFnFactory.ReadULong(deiceData, 0x38);
            R15.Data = DeIceFnFactory.ReadULong(deiceData, 0x34);
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[0x41];
            ret[0x40] = TargetStatus;
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
            DeIceFnFactory.WriteULong(ret, 0x38, R13.Data);
            DeIceFnFactory.WriteULong(ret, 0x34, R15.Data);
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
