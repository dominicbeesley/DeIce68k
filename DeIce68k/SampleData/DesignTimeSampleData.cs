using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeIce68k.ViewModel;
using DeIce68k.ViewModel.Scripts;
using DossySerialPort;

namespace DeIce68k.SampleData
{
    public static class DesignTimeSampleData
    {

        private class DummySerial : IDossySerial
        {
            public int Available => 0;

            public event EventHandler DataReceived;

            public byte PeekByte(int timeoutms = 0)
            {
                throw new TimeoutException();
            }

            public int Read(byte[] b, int length, int timeoutms = 0)
            {
                throw new TimeoutException();
            }

            public byte ReadByte(int timeoutms = 0)
            {
                throw new TimeoutException();
            }

            public void Write(byte[] b, int offset, int length, int timeoutms = 0)
            {
                throw new TimeoutException();
            }
        }

        static DeIceAppModel _app = null;

        public static DeIceAppModel SampleDeIceAppModel { 
            get {
                if (_app == null)
                {
                    _app = new DeIceAppModel(new DummySerial(), null);
                    _app.Watches.Add(new WatchModel(0, "ZERO", WatchType.X08, null));
                    _app.Watches.Add(new WatchModel(16, "sixteen", WatchType.X16, null));
                    _app.Watches.Add(new WatchModel(100, "page1", WatchType.X08, new int[] { 20 } ));
                    IEnumerable<string> errorsR;
                    _app.AddBreakpoint(0xDEADBEEF).ConditionCode = ScriptCompiler.Compile(_app, "return false;", out errorsR);
                    _app.AddBreakpoint(0x0B00B135).Enabled = false;
                    _app.AddBreakpoint(0x00154BE7).Selected = true;
                    _app.AddBreakpoint(0x008D0812);
                    _app.Symbols.Add("bob", 0x8d080c);
                    _app.Symbols.Add("sheila_crtc_reg", 0xFFFFFE00);
                    _app.Symbols.Add("CRTC_R0", 0xFFFFFE00);
                    _app.Symbols.Add("sheila_crtc_rw", 0xFFFFFE01);
                    _app.Symbols.Add("CRTC_R1", 0xFFFFFE00);
                    _app.DisassMemBlock = new DisassMemBlock(
                        _app,
                        0x8d080c,
                        new byte[]
                        {
                            0x52, 0x01, 0x11, 0xc1, 0xfe, 0x00, 0x11, 0xC0, 0xFE, 0x01, 0x4e, 0x75, 0x99, 0x99, 0x99, 0x99
                        }
                    );

                    Task.Run(async delegate
                    {
                        Random r = new Random();
                        while (true)
                        {
                            await Task.Delay(500);
                            switch (r.Next(20))
                            {
                                case 0:
                                    _app.Regs.D0.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 1:
                                    _app.Regs.D1.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 2:
                                    _app.Regs.D2.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 3:
                                    _app.Regs.D3.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 4:
                                    _app.Regs.D4.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 5:
                                    _app.Regs.D5.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 6:
                                    _app.Regs.D6.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 7:
                                    _app.Regs.D7.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 8:
                                    _app.Regs.A0.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 9:
                                    _app.Regs.A1.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 10:
                                    _app.Regs.A2.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 11:
                                    _app.Regs.A3.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 12:
                                    _app.Regs.A4.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 13:
                                    _app.Regs.A5.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 14:
                                    _app.Regs.A6.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 15:
                                    _app.Regs.A7u.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 16:
                                    _app.Regs.A7s.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 17:
                                    _app.Regs.PC.Data = (uint)r.Next(int.MinValue, int.MaxValue);
                                    break;
                                case 19:
                                    _app.Regs.SR.Data = (uint)r.Next(0xFFFF);
                                    break;
                            }
                        }
                    });
                }
                return _app;
            } 
        }

        public static DisassMemBlock DisassMem => SampleDeIceAppModel.DisassMemBlock;

        public static RegisterSetModel SampleDataRegisterSetModel { get { return SampleDeIceAppModel.Regs; } }

        public static StatusRegisterBitsModel SampleStatusRegisterBit { get { return SampleDeIceAppModel.Regs.StatusBits.FirstOrDefault(); } }


        public static RegisterModel RegisterModelTestWord = new RegisterModel("XX", RegisterSize.Word, 0xBEEF);
        public static RegisterModel RegisterModelTestLong = new RegisterModel("XX", RegisterSize.Word, 0xDEADBEEF);

        public static ObservableCollection<WatchModel> SamplesWatches { get { return SampleDeIceAppModel.Watches; } }

        public static ReadOnlyObservableCollection<BreakpointModel> SamplesBreakpoints { get { return SampleDeIceAppModel.Breakpoints; } }

        public static List<string> SampleErrors { get { return new List<string>(new[] { "Error 1", "error 2" }); } }
    }
}
