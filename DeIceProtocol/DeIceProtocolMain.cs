using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DossySerialPort;

namespace DeIceProtocol
{

    public class DeIceOobDataReceivedEventArgs : EventArgs
    {
        public string Data { get; }

        public DeIceOobDataReceivedEventArgs(string data)
            : base()
        {
            this.Data = data;
        }
    }

    public delegate void DeIceOobDataReceivedEvent(object sender, DeIceOobDataReceivedEventArgs e);

    public class DeIceFunctionReceivedEventArgs : EventArgs
    {
        public DeIceFnReplyBase Function { get; }

        public DeIceFunctionReceivedEventArgs(DeIceFnReplyBase fn)
            : base()
        {
            this.Function = fn;
        }
    }

    public delegate void DeIceFunctionReceivedEvent(object sender, DeIceFunctionReceivedEventArgs e);


    public class DeIceComErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public DeIceComErrorEventArgs(Exception e)
            : base()
        {
            this.Exception = e;
        }
    }

    public delegate void DeIceCommErrorEvent(object sender, DeIceComErrorEventArgs e);


    public class DeIceProtocolMain
    {
        const int RESPONSE_TIMEOUT = 100; //how long to wait until a command's response time's out
        const int SHORT_TIMEOUT = 100; // how long to wait for more oob data before showing
        const int LONG_TIMEOUT = 2000; // how long to wait for other threads to release
        const int BUF_SIZE = 256;
        const int OOB_MAX = 16384;  //maximum size off OOB block

        public event DeIceOobDataReceivedEvent OobDataReceived;
        public event DeIceCommErrorEvent CommError;
        public event DeIceFunctionReceivedEvent FunctionReceived;

        IDossySerial _serial = null;

        private Semaphore _runSemaphore = new(1, 1);
        bool _expecting_reply = false;

        public DeIceProtocolMain(IDossySerial serial)
        {
            this._serial = serial;

            _serial.DataReceived += _serial_DataReceived;

        }

        private void _serial_DataReceived(object o, EventArgs e) // object sender, SerialDataReceivedEventArgs e)
        {
            while (!_expecting_reply)
            {
                if (!_runSemaphore.WaitOne(0))
                    return;
                bool claimed = true;
                try
                {
                    if (_serial.Available <= 0)
                        return;

                    try
                    {
                        byte firstbyte = _serial.ReadByte(SHORT_TIMEOUT);
                        if (firstbyte >= DeIceProtoConstants.FN_MIN || firstbyte == DeIceProtoConstants.FN_ERROR)
                        {
                            var cmd = DoCommandReadInt(firstbyte, true);
                            _runSemaphore.Release();
                            claimed = false;
                            RaiseFunctionReceived(cmd);
                        }
                        else
                        {
                            DoOobRead_int(firstbyte);                            
                        }
                    }
                    catch (TimeoutException) //timeout
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        CommError?.Invoke(this, new DeIceComErrorEventArgs(ex));
                    }
                }
                finally
                {
                    if (claimed)
                        _runSemaphore.Release();
                }
            }
        }

        public T SendReqExpectStatusByte<T>(DeIceFnReqBase fn) where T : DeIceFnReplyStatusBase
        {
            T ret = SendReqExpectReply<T>(fn);
            if (!ret?.Success ?? false)
            {
                throw new DeIceProtocolException($"{fn.GetType().FullName} returned failure status");
            }
            return ret;
        }


        public T SendReqExpectReply<T>(DeIceFnReqBase fn) where T : DeIceFnReplyBase
        {
            if (!_runSemaphore.WaitOne(LONG_TIMEOUT))
                throw new TimeoutException();
            try
            {
                _expecting_reply = true;
                try
                {
                    byte[] buf = DeIceFnFactory.CreateDataFromReq(fn);
                    _serial.Write(buf, 0, buf.Length, LONG_TIMEOUT);
                    var r = DoCommandReadInt();
                    if (r is DeIceFnReplyError)
                        throw new DeIceProtocolException($"Error returned from client, expected a {typeof(T).FullName}, received a {r?.GetType()?.Name ?? null}");
                    if (r is not T)
                        throw new DeIceProtocolException($"Unexpected return from client, expected a { typeof(T).FullName }, received a { r?.GetType()?.Name ?? null }");
                    return (T)r;
                }
                finally
                {
                    _expecting_reply = false;
                    _runSemaphore.Release();
                    if (_serial.Available > 0)
                        _serial_DataReceived(_serial, EventArgs.Empty);
                }
            } catch (Exception ex)
            {
                throw new Exception($"Error executing {fn?.GetType()?.FullName ?? "-null-"} : {ex.Message}", ex);
            }
        }

        public void SendReq(DeIceFnReqBase fn)
        {
            if (!_runSemaphore.WaitOne(LONG_TIMEOUT))
                throw new TimeoutException();

            try
            {
                byte[] buf = DeIceFnFactory.CreateDataFromReq(fn);
                        _serial.Write(buf, 0, buf.Length, SHORT_TIMEOUT);
                        return;
            }
            finally
            {
                _runSemaphore.Release();
                if (_serial.Available > 0)
                    _serial_DataReceived(_serial, EventArgs.Empty);
            }
        }

        const int READMEMMAX = 32;

        public void ReadMemBlock(uint addr, byte[] disdat, int offset, int len)
        {
            int offs = offset;

            while (len > 0)
            {
                byte l2;
                if (len > READMEMMAX)
                    l2 = READMEMMAX;
                else
                    l2 = (byte)len;

                var rm = this.SendReqExpectReply<DeIceFnReplyReadMem>(new DeIceFnReqReadMem() { Address = addr, Len = l2 });

                Array.Copy(rm.Data, 0, disdat, offs, l2);

                len -= l2;
                addr += l2;
                offs += l2;
            }

        }

        private readonly static int MAXOOB = 1024;
        MemoryStream oobms = new MemoryStream();
        //TODO: dispose oobcs
        CancellationTokenSource oobcs = new CancellationTokenSource();

        private void DoOobRead_int(byte first)
        {
            oobcs.Cancel();
            if (first == 0x0D || first == 0x0A)
                return;

            oobms.WriteByte(first);

            var sendStr = () =>
            {
                var r = Encoding.ASCII.GetString(oobms.ToArray());
                oobms.SetLength(0);
                if (r != "")
                    RaiseOobData(r);
            };

            while (true)
            {
                try
                {
                    byte b = _serial.PeekByte(SHORT_TIMEOUT);
                    if (b >= DeIceProtoConstants.FN_MIN || b == DeIceProtoConstants.FN_ERROR)
                    {
                        sendStr();

                        if (_serial.Available > 0)
                        {
                            // There's a command pending but it might be an ExpectReply method on another
                            // thread, yield up for a time to allow the other thread to pick it up
                            // but try again later in case it is an unsolicited interrupt reply

                            Task.Run(async () =>
                            {
                                await Task.Delay(SHORT_TIMEOUT);
                                _serial_DataReceived(_serial, EventArgs.Empty);
                            });
                        }
                        return;
                    }
                    else
                    {
                        b = _serial.ReadByte(SHORT_TIMEOUT);
                        if (b == 0xD || b == 0xA || oobms.Length >= MAXOOB)
                        {
                            sendStr();
                            return;
                        }
                        else
                        {
                            oobms.WriteByte(b);
                        }
                    }
                }
                catch (TimeoutException)
                {
                    if (oobms.Length > 0)
                        Task.Delay(LONG_TIMEOUT, oobcs.Token).ContinueWith((task) => {
                            //TODO: semaphore here?
                            sendStr();
                        });

                    return;
                }
            }

        }

        private void RaiseOobData(string data)
        {
            Task.Run(() => { 
            foreach (var l in data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries))
                OobDataReceived?.Invoke(this, new DeIceOobDataReceivedEventArgs(l));
            });
        }

        private DeIceFnReplyBase DoCommandReadInt(byte first = 0, bool hasFirst = false)
        {
            byte[] buf = new byte[BUF_SIZE];
            int bufptr = 0;
            byte lenctr = 0;
            if (!hasFirst)
            {
                Stopwatch sw = Stopwatch.StartNew();

                while (true)
                {
                    first = _serial.ReadByte(SHORT_TIMEOUT);
                    if (first < DeIceProtoConstants.FN_MIN)
                        DoOobRead_int(first);
                    else
                        break;

                    if (sw.ElapsedMilliseconds >= SHORT_TIMEOUT)
                        throw new TimeoutException();
                }
            }
            bufptr = 0;
            buf[bufptr++] = first;
            byte ck = first;

            // get length
            lenctr = _serial.ReadByte(SHORT_TIMEOUT);
            buf[bufptr++] = lenctr;
            ck += lenctr;

            byte i;

            for (byte j = 0; j < lenctr; j++)
            {
                i = _serial.ReadByte(SHORT_TIMEOUT);
                if (bufptr >= BUF_SIZE - 1)
                    throw new Exception("Buffer full");
                buf[bufptr++] = (byte)i;
                ck += (byte)i;
            }

            i = _serial.ReadByte(SHORT_TIMEOUT);
            buf[bufptr++] = i;

            ck += i;
            if (ck != 0)
                throw new Exception("Bad checksum");

            return DeIceFnFactory.CreateReplyFromData(buf.Take(lenctr + 3).ToArray());
        }

        private void RaiseFunctionReceived(DeIceFnReplyBase deIceFnBase)
        {
            Task.Run(() => FunctionReceived?.Invoke(this, new DeIceFunctionReceivedEventArgs(deIceFnBase)));
        }


        /// <summary>
        /// Flush the input buffer
        /// </summary>
        public void Flush()
        {
            try
            {
                byte[] buf = new byte[256];
                while (_serial.Available > 0)
                {
                    _serial.Read(buf, 256, 1);
                }
            }
            catch (Exception) { }
        }
    }
}
