using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeIce68k.ViewModel;
using DeIce68k.ViewModel.Scripts;
using DeIceProtocol;
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

            public void Dispose() { }
        }

        static DeIceAppModel _app = null;

        public static DeIceAppModel SampleDeIceAppModel_68k { 
            get {
                if (_app == null)
                {
                    _app = new DeIceAppModel(new DummySerial(), null, true, new DeIceFnReplyGetStatus(
                        DeIceProtoConstants.HOST_68k, 0x80, 
                        DeIceTargetOptionFlags.HasFNCall | DeIceTargetOptionFlags.HasFNResetTarget | DeIceTargetOptionFlags.HasFNStep | DeIceTargetOptionFlags.HasFNStopTarget,
                        0,0x8000, new byte [] { 0x4E, 0x4F }, "TEST 68k", 0, 0)
                        );
                    
                    _app.Watches.Add(new WatchModel(0, "ZERO", WatchType.X08, null));
                    _app.Watches.Add(new WatchModel(16, "sixteen", WatchType.X16, null));
                    _app.Watches.Add(new WatchModel(100, "page1", WatchType.X08, new uint[] { 20 } ));
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
                            if (_app.Regs != null)
                            {
                                byte[] regs = _app.Regs.ToDeIceProtcolRegData();
                                if (regs.Length > 0)
                                {
                                    regs[r.Next(regs.Length)] = (byte)r.Next(255);
                                }
                                _app.Regs.FromDeIceProtocolRegData(regs);
                            }
                        }
                    });
                }
                return _app;
            } 
        }

        public static DeIceAppModel SampleDeIceAppModel_x86_16
        {
            get
            {
                if (_app == null)
                {
                    _app = new DeIceAppModel(new DummySerial(), null, true, new DeIceFnReplyGetStatus(
                        DeIceProtoConstants.HOST_x86_16, 0x80,
                        DeIceTargetOptionFlags.HasFNCall | DeIceTargetOptionFlags.HasFNResetTarget | DeIceTargetOptionFlags.HasFNStep | DeIceTargetOptionFlags.HasFNStopTarget,
                        0, 0x8000, new byte[] { 0x4E, 0x4F }, "TEST Intel x86 16", 0, 0)
                        );

                    _app.Watches.Add(new WatchModel(0, "ZERO", WatchType.X08, null));
                    _app.Watches.Add(new WatchModel(16, "sixteen", WatchType.X16, null));
                    _app.Watches.Add(new WatchModel(100, "page1", WatchType.X08, new uint[] { 20 }));
                    IEnumerable<string> errorsR;
                    _app.AddBreakpoint(0xDEADBEEF).ConditionCode = ScriptCompiler.Compile(_app, "return false;", out errorsR);
                    _app.AddBreakpoint(0x0B00B135).Enabled = false;
                    _app.AddBreakpoint(0x00154BE7).Selected = true;
                    _app.AddBreakpoint(0x008D0812);
                    _app.Symbols.Add(".excl", 0xFC0019B9);
                    _app.Symbols.Add(".ex", 0xFC0019C4);
                    _app.Symbols.Add(".ex_nokeys", 0xFC0019CA);
                    _app.Symbols.Add("dom_keyb_auto_off", 0xFC0019D1);
                    _app.Symbols.Add("dom_keyb_auto_on", 0xFC0019EC);
                    _app.Symbols.Add("io_SHEILA_SYSVIA_DDRA", 0xFE43);
                    _app.Symbols.Add("io_SHEILA_SYSVIA_ORA_NH", 0xFE4F);
                    _app.Symbols.Add("io_SHEILA_SYSVIA_ORB", 0xFE40);
                    _app.Symbols.Add("io_SHEILA_SYSVIA_IFR", 0xFE4D);
                    _app.DisassMemBlock = new DisassMemBlock(
                        _app,
                        0xFC0019B9,
                        new byte[]
                        {
                            0x50, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0x58, 0xE8, 0x28, 0x00, 0x1F, 0x5A, 0x5B, 0x58, 0x9D, 0xC3, 0x31, 0xC0, 0xA3, 0x86, 0x00, 0xEB, 0xE8,
                            0x50, 0xBA, 0x43, 0xFE, 0xB0, 0x7F, 0xEE, 0xBA, 0x4F, 0xFE, 0xB0, 0x0F, 0xEE, 0xBA, 0x40, 0xFE, 0xB0, 0x03, 0xEE, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0x58, 0xC3, 0x50, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0xBA, 0x40, 0xFE, 0xB0, 0x0B, 0xEE, 0xBA, 0x43, 0xFE, 0x31, 0xC0, 0xEE, 0x58, 0xC3
                        }
                    );

                    Task.Run(async delegate
                    {
                        Random r = new Random();
                        while (true)
                        {
                            await Task.Delay(500);
                            if (_app.Regs != null)
                            {
                                byte[] regs = _app.Regs.ToDeIceProtcolRegData();
                                if (regs.Length > 0)
                                {
                                    regs[r.Next(regs.Length)] = (byte)r.Next(255);
                                }
                                _app.Regs.FromDeIceProtocolRegData(regs);
                            }
                        }
                    });
                }
                return _app;
            }
        }


        public static DisassMemBlock DisassMem => SampleDeIceAppModel_x86_16.DisassMemBlock;

        public static RegisterSetModelBase SampleDataRegisterSetModel_x86_16 { get { return SampleDeIceAppModel_x86_16.Regs; } }

        public static RegisterSetModelBase SampleDataRegisterSetModel_68k { get { return SampleDeIceAppModel_68k.Regs; } }


        public static StatusRegisterBitsModel SampleStatusRegisterBit { get { return SampleDeIceAppModel_68k.Regs.StatusBits.FirstOrDefault(); } }


        public static RegisterModel RegisterModelTestWord = new RegisterModel("XX", RegisterSize.Word, 0xBEEF);
        public static RegisterModel RegisterModelTestLong = new RegisterModel("XX", RegisterSize.Word, 0xDEADBEEF);

        public static ObservableCollection<WatchModel> SamplesWatches { get { return SampleDeIceAppModel_68k.Watches; } }

        public static ReadOnlyObservableCollection<BreakpointModel> SamplesBreakpoints { get { return SampleDeIceAppModel_68k.Breakpoints; } }

        public static List<string> SampleErrors { get { return new List<string>(new[] { "Error 1", "error 2" }); } }
    }
}
