using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeIce68k.ViewModel;
using DeIce68k.ViewModel.Scripts;
using DeIceProtocol;
using DisassShared;
using Disass68k;
using DossySerialPort;
using DisassX86;
using DisassArm;

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

        static DeIceAppModel _app68k = null;

        public static DeIceAppModel SampleDeIceAppModel_68k { 
            get {
                if (_app68k == null)
                {
                    _app68k = new DeIceAppModel(new DummySerial(), null, true, new DeIceFnReplyGetStatus(
                        DeIceProtoConstants.HOST_68k, 0x80, 
                        DeIceTargetOptionFlags.HasFNCall | DeIceTargetOptionFlags.HasFNResetTarget | DeIceTargetOptionFlags.HasFNStep | DeIceTargetOptionFlags.HasFNStopTarget,
                        0,0x8000, new byte [] { 0x4E, 0x4F }, "TEST 68k", 0, 0)
                        );
                    
                    _app68k.Watches.Add(new WatchModel(new Address68K(0), "ZERO", WatchType.X08, null));
                    _app68k.Watches.Add(new WatchModel(new Address68K(16), "sixteen", WatchType.X16, null));
                    _app68k.Watches.Add(new WatchModel(new Address68K(100), "page1", WatchType.X08, new uint[] { 20 } ));
                    IEnumerable<string> errorsR;
                    _app68k.AddBreakpoint(new Address68K(0xDEADBEEF)).ConditionCode = ScriptCompiler.Compile(_app68k, "return false;", out errorsR);
                    _app68k.AddBreakpoint(new Address68K(0x0B00B135)).Enabled = false;
                    _app68k.AddBreakpoint(new Address68K(0x00154BE7)).Selected = true;
                    _app68k.AddBreakpoint(new Address68K(0x008D0812));
                    _app68k.Symbols.Add("bob", new Address68K(0x8d080c), SymbolType.Pointer);
                    _app68k.Symbols.Add("sheila_crtc_reg", new Address68K(0xFFFFFE00), SymbolType.Pointer);
                    _app68k.Symbols.Add("CRTC_R0", new Address68K(0xFFFFFE00), SymbolType.Pointer);
                    _app68k.Symbols.Add("sheila_crtc_rw", new Address68K(0xFFFFFE01), SymbolType.Pointer);
                    _app68k.Symbols.Add("CRTC_R1", new Address68K(0xFFFFFE00), SymbolType.Pointer);
                    _app68k.DisassMemBlock = new DisassMemBlock(
                        _app68k,
                        new Address68K(0x8d080c),
                        new byte[]
                        {
                            0x52, 0x01, 0x11, 0xc1, 0xfe, 0x00, 0x11, 0xC0, 0xFE, 0x01, 0x4e, 0x75, 0x99, 0x99, 0x99, 0x99
                        },
                        new Disass68k.Disass68k()
                    );

                    Task.Run(async delegate
                    {
                        Random r = new Random();
                        while (true)
                        {
                            await Task.Delay(500);
                            if (_app68k.Regs != null)
                            {
                                byte[] regs = _app68k.Regs.ToDeIceProtcolRegData();
                                if (regs.Length > 0)
                                {
                                    regs[r.Next(regs.Length)] = (byte)r.Next(255);
                                }
                                _app68k.Regs.FromDeIceProtocolRegData(regs);
                            }
                        }
                    });
                }
                return _app68k;
            } 
        }

        static DeIceAppModel _appx86_16 = null;

        public static DeIceAppModel SampleDeIceAppModel_x86_16
        {
            get
            {
                if (_appx86_16 == null)
                {
                    _appx86_16 = new DeIceAppModel(new DummySerial(), null, true, new DeIceFnReplyGetStatus(
                        DeIceProtoConstants.HOST_x86_16, 0x80,
                        DeIceTargetOptionFlags.HasFNCall | DeIceTargetOptionFlags.HasFNResetTarget | DeIceTargetOptionFlags.HasFNStep | DeIceTargetOptionFlags.HasFNStopTarget,
                        0, 0x8000, new byte[] { 0x4E, 0x4F }, "TEST Intel x86 16", 0, 0)
                        );

                    _appx86_16.Watches.Add(new WatchModel(new AddressX86(0,0), "ZERO", WatchType.X08, null));
                    _appx86_16.Watches.Add(new WatchModel(new AddressX86(0x1234,16), "sixteen", WatchType.X16, null));
                    _appx86_16.Watches.Add(new WatchModel(new AddressX86(0xFFFF,0xFFFF), "page1", WatchType.X08, new uint[] { 20 }));
                    IEnumerable<string> errorsR;
                    _appx86_16.AddBreakpoint(new AddressX86(0xDEAD, 0xBEEF)).ConditionCode = ScriptCompiler.Compile(_appx86_16, "return false;", out errorsR);
                    _appx86_16.AddBreakpoint(new AddressX86(0x0B00, 0xB135)).Enabled = false;
                    _appx86_16.AddBreakpoint(new AddressX86(0x0015, 0x4BE7)).Selected = true;
                    _appx86_16.AddBreakpoint(new AddressX86(0x008D, 0x0812));
                    _appx86_16.Symbols.Add(".excl", new AddressX86(0xFC00, 0x19B9), SymbolType.Pointer);
                    _appx86_16.Symbols.Add(".ex", new AddressX86(0xFC00, 0x19C4), SymbolType.Pointer);
                    _appx86_16.Symbols.Add(".ex_nokeys", new AddressX86(0xFC00, 0x19CA), SymbolType.Pointer);
                    _appx86_16.Symbols.Add("dom_keyb_auto_off", new AddressX86(0xFC00, 0x19D1), SymbolType.Pointer);
                    _appx86_16.Symbols.Add("dom_keyb_auto_on", new AddressX86(0xFC00, 0x19EC), SymbolType.Pointer);
                    _appx86_16.Symbols.Add("io_SHEILA_SYSVIA_DDRA", new AddressX86(0, 0xFE43), SymbolType.Port);
                    _appx86_16.Symbols.Add("io_SHEILA_SYSVIA_ORA_NH", new AddressX86(0, 0xFE4F), SymbolType.Port);
                    _appx86_16.Symbols.Add("io_SHEILA_SYSVIA_ORB", new AddressX86(0, 0xFE40), SymbolType.Port);
                    _appx86_16.Symbols.Add("io_SHEILA_SYSVIA_IFR", new AddressX86(0, 0xFE4D), SymbolType.Port);
                    _appx86_16.DisassMemBlock = new DisassMemBlock(
                        _appx86_16,
                        new AddressX86(0xFC00, 0x19B9),
                        new byte[]
                        {
                            0x50, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0x58, 0xE8, 0x28, 0x00, 0x1F, 0x5A, 0x5B, 0x58, 0x9D, 0xC3, 0x31, 0xC0, 0xA3, 0x86, 0x00, 0xEB, 0xE8,
                            0x50, 0xBA, 0x43, 0xFE, 0xB0, 0x7F, 0xEE, 0xBA, 0x4F, 0xFE, 0xB0, 0x0F, 0xEE, 0xBA, 0x40, 0xFE, 0xB0, 0x03, 0xEE, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0x58, 0xC3, 0x50, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0xBA, 0x40, 0xFE, 0xB0, 0x0B, 0xEE, 0xBA, 0x43, 0xFE, 0x31, 0xC0, 0xEE, 0x58, 0xC3
                        },
                        new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_186)
                    );

                    Task.Run(async delegate
                    {
                        Random r = new Random();
                        while (true)
                        {
                            await Task.Delay(500);
                            if (_appx86_16.Regs != null)
                            {
                                byte[] regs = _appx86_16.Regs.ToDeIceProtcolRegData();
                                if (regs.Length > 0)
                                {
                                    regs[r.Next(regs.Length)] = (byte)r.Next(255);
                                }
                                _appx86_16.Regs.FromDeIceProtocolRegData(regs);
                            }
                        }
                    });
                }
                return _appx86_16;
            }
        }

        static DeIceAppModel _appArm2 = null;

        public static DeIceAppModel SampleDeIceAppModel_Arm2
        {
            get
            {
                if (_appArm2 == null)
                {
                    _appArm2 = new DeIceAppModel(new DummySerial(), null, true, new DeIceFnReplyGetStatus(
                        DeIceProtoConstants.HOST_ARM2, 0x80,
                        DeIceTargetOptionFlags.HasFNCall | DeIceTargetOptionFlags.HasFNResetTarget | DeIceTargetOptionFlags.HasFNStep | DeIceTargetOptionFlags.HasFNStopTarget,
                        0, 0x8000, new byte[] { 0x4E, 0x4F }, "TEST ARM 2", 0, 0)
                        );

                    _appArm2.Watches.Add(new WatchModel(new AddressArm2(0), "ZERO", WatchType.X08, null));
                    _appArm2.Watches.Add(new WatchModel(new AddressArm2(16), "sixteen", WatchType.X16, null));
                    _appArm2.Watches.Add(new WatchModel(new AddressArm2(100), "page1", WatchType.X08, new uint[] { 20 }));
                    IEnumerable<string> errorsR;
                    _appArm2.AddBreakpoint(new AddressArm2(0xDEADBEEF)).ConditionCode = ScriptCompiler.Compile(_appArm2, "return false;", out errorsR);
                    _appArm2.AddBreakpoint(new AddressArm2(0x0B00B135)).Enabled = false;
                    _appArm2.AddBreakpoint(new AddressArm2(0x00154BE7)).Selected = true;
                    _appArm2.AddBreakpoint(new AddressArm2(0x008D0812));
                    _appArm2.Symbols.Add(".excl", new AddressArm2(0xFC0019B9), SymbolType.Pointer);
                    _appArm2.Symbols.Add(".ex", new AddressArm2(0xFC0019C4), SymbolType.Pointer);
                    _appArm2.Symbols.Add(".ex_nokeys", new AddressArm2(0xFC0019CA), SymbolType.Pointer);
                    _appArm2.Symbols.Add("dom_keyb_auto_off", new AddressArm2(0xFC0019D1), SymbolType.Pointer);
                    _appArm2.Symbols.Add("dom_keyb_auto_on", new AddressArm2(0xFC0019EC), SymbolType.Pointer);
                    _appArm2.Symbols.Add("io_SHEILA_SYSVIA_DDRA", new AddressArm2(0x03FFFE43), SymbolType.Port);
                    _appArm2.Symbols.Add("io_SHEILA_SYSVIA_ORA_NH", new AddressArm2(0x03FFFE4F), SymbolType.Port);
                    _appArm2.Symbols.Add("io_SHEILA_SYSVIA_ORB", new AddressArm2(0x03FFFE40), SymbolType.Port);
                    _appArm2.Symbols.Add("io_SHEILA_SYSVIA_IFR", new AddressArm2(0x03FFFE4D), SymbolType.Port);
                    _appArm2.DisassMemBlock = new DisassMemBlock(
                        _appArm2,
                        new AddressArm2(0xFC0019B9),
                        new byte[]
                        {
                            0x50, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0x58, 0xE8, 0x28, 0x00, 0x1F, 0x5A, 0x5B, 0x58, 0x9D, 0xC3, 0x31, 0xC0, 0xA3, 0x86, 0x00, 0xEB, 0xE8,
                            0x50, 0xBA, 0x43, 0xFE, 0xB0, 0x7F, 0xEE, 0xBA, 0x4F, 0xFE, 0xB0, 0x0F, 0xEE, 0xBA, 0x40, 0xFE, 0xB0, 0x03, 0xEE, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0x58, 0xC3, 0x50, 0xBA, 0x4D, 0xFE, 0xB0, 0x01, 0xEE, 0xBA, 0x40, 0xFE, 0xB0, 0x0B, 0xEE, 0xBA, 0x43, 0xFE, 0x31, 0xC0, 0xEE, 0x58, 0xC3
                        },
                        new DisassX86.DisassX86(DisassX86.DisassX86.API.cpu_186)
                    );

                    Task.Run(async delegate
                    {
                        Random r = new Random();
                        while (true)
                        {
                            await Task.Delay(500);
                            if (_appArm2.Regs != null)
                            {
                                byte[] regs = _appArm2.Regs.ToDeIceProtcolRegData();
                                if (regs.Length > 0)
                                {
                                    regs[r.Next(regs.Length)] = (byte)r.Next(255);
                                }
                                _appArm2.Regs.FromDeIceProtocolRegData(regs);
                            }
                        }
                    });
                }
                return _appArm2;
            }
        }



        public static DisassMemBlock DisassMem => SampleDeIceAppModel_x86_16.DisassMemBlock;

        public static RegisterSetModelBase SampleDataRegisterSetModel_x86_16 { get { return SampleDeIceAppModel_x86_16.Regs; } }

        public static RegisterSetModelBase SampleDataRegisterSetModel_68k { get { return SampleDeIceAppModel_68k.Regs; } }

        public static RegisterSetModelBase SampleDataRegisterSetModel_Arm2 { get { return SampleDeIceAppModel_Arm2.Regs; } }

        public static StatusRegisterBitsModel SampleStatusRegisterBit { get { return SampleDeIceAppModel_68k.Regs.StatusBits.FirstOrDefault(); } }


        public static RegisterModel RegisterModelTestWord = new RegisterModel("XX", RegisterSize.Word, 0xBEEF);
        public static RegisterModel RegisterModelTestLong = new RegisterModel("XX", RegisterSize.Word, 0xDEADBEEF);

        public static ObservableCollection<WatchModel> SamplesWatches { get { return SampleDeIceAppModel_68k.Watches; } }

        public static ReadOnlyObservableCollection<BreakpointModel> SamplesBreakpoints { get { return SampleDeIceAppModel_68k.Breakpoints; } }

        public static List<string> SampleErrors { get { return new List<string>(new[] { "Error 1", "error 2" }); } }
    }
}
