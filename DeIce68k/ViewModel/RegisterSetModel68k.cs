﻿using DeIceProtocol;
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
    public class RegisterSetModel68k : RegisterSetModelBase
    {
        public RegisterModel D0 { get; }
        public RegisterModel D1 { get; }
        public RegisterModel D2 { get; }
        public RegisterModel D3 { get; }
        public RegisterModel D4 { get; }
        public RegisterModel D5 { get; }
        public RegisterModel D6 { get; }
        public RegisterModel D7 { get; }

        public RegisterModel A0 { get; }
        public RegisterModel A1 { get; }
        public RegisterModel A2 { get; }
        public RegisterModel A3 { get; }
        public RegisterModel A4 { get; }
        public RegisterModel A5 { get; }
        public RegisterModel A6 { get; }
        public RegisterModel A7u { get; }
        public RegisterModel A7s { get; }

        public RegisterModel PC { get; }
        public RegisterModel SR { get; }

        public override bool CanTrace => true;

        public override uint PCValue
        {
            get { return PC.Data; }
            set { PC.Data = value; }
        }

        public RegisterSetModel68k(DeIceAppModel _parent)
        {
            Parent = _parent;

            D0 = new RegisterModel("D0", RegisterSize.Long, 0);
            D1 = new RegisterModel("D1", RegisterSize.Long, 0);
            D2 = new RegisterModel("D2", RegisterSize.Long, 0);
            D3 = new RegisterModel("D3", RegisterSize.Long, 0);
            D4 = new RegisterModel("D4", RegisterSize.Long, 0);
            D5 = new RegisterModel("D5", RegisterSize.Long, 0);
            D6 = new RegisterModel("D6", RegisterSize.Long, 0);
            D7 = new RegisterModel("D7", RegisterSize.Long, 0);

            A0 = new RegisterModel("A0", RegisterSize.Long, 0);
            A1 = new RegisterModel("A1", RegisterSize.Long, 0);
            A2 = new RegisterModel("A2", RegisterSize.Long, 0);
            A3 = new RegisterModel("A3", RegisterSize.Long, 0);
            A4 = new RegisterModel("A4", RegisterSize.Long, 0);
            A5 = new RegisterModel("A5", RegisterSize.Long, 0);
            A6 = new RegisterModel("A6", RegisterSize.Long, 0);
            A7u = new RegisterModel("A7u", RegisterSize.Long, 0);
            A7s = new RegisterModel("A7s", RegisterSize.Long, 0);

            PC = new RegisterModel("PC", RegisterSize.Long, 0);
            SR = new RegisterModel("SR", RegisterSize.Word, 0);

            StatusBits = new ReadOnlyObservableCollection<StatusRegisterBitsModel>(
                new ObservableCollection<StatusRegisterBitsModel>(
            new StatusRegisterBitsModel[]
            {
                new(this) { BitIndex=15, Label="T", Name="Trace" },
                new(this) { BitIndex=14, Label="-", Name="Uk 14" },
                new(this) { BitIndex=13, Label="S", Name="Supervisor" },
                new(this) { BitIndex=12, Label="-", Name="Uk 12" },
                new(this) { BitIndex=11, Label="-", Name="Uk 11" },
                new(this) { BitIndex=10, Label="I2", Name="Int.2" },
                new(this) { BitIndex=9, Label="I1", Name="Int.1" },
                new(this) { BitIndex=8, Label="I0", Name="Int.0" },
                new(this) { BitIndex=7, Label="-", Name="Uk 7" },
                new(this) { BitIndex=6, Label="-", Name="Uk 6" },
                new(this) { BitIndex=5, Label="-", Name="Uk 5" },
                new(this) { BitIndex=4, Label="X", Name="Extend" },
                new(this) { BitIndex=3, Label="N", Name="Negative" },
                new(this) { BitIndex=2, Label="Z", Name="Zero" },
                new(this) { BitIndex=1, Label="V", Name="Overflow" },
                new(this) { BitIndex=0, Label="C", Name="Carry" }
            }));

            SR.PropertyChanged += SR_PropertyChanged;

            foreach (var sb in StatusBits)
            {
                sb.PropertyChanged += Sb_PropertyChanged;
            }

            UpdateStatusBits();
        }

        public override void FromDeIceProtocolRegData(byte[] deiceData)
        {
            if (deiceData.Length < 0x4C)
                throw new ArgumentException("data too short FN_READ_RG/FN_RUN_TARG reply");

            TargetStatus = deiceData[0x00];
            A7u.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x02);
            A7s.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x06);
            D0.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x0A);
            D1.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x0E);
            D2.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x12);
            D3.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x16);
            D4.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x1A);
            D5.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x1E);
            D6.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x22);
            D7.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x26);
            A0.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x2A);
            A1.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x2E);
            A2.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x32);
            A3.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x36);
            A4.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x3A);
            A5.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x3E);
            A6.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x42);
            SR.Data = DeIceFnFactory.ReadBEUShort(deiceData, 0x46);
            PC.Data = DeIceFnFactory.ReadBEULong(deiceData, 0x48);
        }

        public override byte[] ToDeIceProtcolRegData()
        {
            byte [] ret = new byte[0x4C];
            ret[0] = TargetStatus;
            DeIceFnFactory.WriteBEULong(ret, 0x02, A7u.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x06, A7s.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x0A, D0.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x0E, D1.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x12, D2.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x16, D3.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x1A, D4.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x1E, D5.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x22, D6.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x26, D7.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x2A, A0.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x2E, A1.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x32, A2.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x36, A3.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x3A, A4.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x3E, A5.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x42, A6.Data);
            DeIceFnFactory.WriteBEUShort(ret, 0x46, (ushort)SR.Data);
            DeIceFnFactory.WriteBEULong(ret, 0x48, PC.Data);
            return ret;
        }

        private void Sb_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            StatusRegisterBitsModel sb = sender as StatusRegisterBitsModel;
            if (sb is not null)
            {
                uint mask = (uint)(1 << sb.BitIndex);
                SR.Data = (SR.Data & ~mask) | ((sb.Data) ? mask : 0);
            }
        }

        private void SR_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterModel.Data))
            {
                UpdateStatusBits();
            }
        }

        private void UpdateStatusBits()
        {
            var sr = SR.Data;
            foreach (var sb in StatusBits)
            {
                sb.Data = (sr & 1 << sb.BitIndex) != 0;
            }
        }

        public override bool SetTrace(bool trace)
        {
            bool ret = (SR.Data & 0x8000) != 0;
            if (trace && (SR.Data & 0x8000) == 0)
            {
                SR.Data |= 0x8000;
            } else if (!trace && (SR.Data & 0x8000) != 0)
            {
                SR.Data &= ~(uint)0x8000;
            }

            return ret;
        }
    }
}
