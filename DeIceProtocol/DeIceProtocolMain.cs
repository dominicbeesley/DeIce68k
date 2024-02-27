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


    public class DeIceProtocolMain : IDisposable
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

        object _bgReadLock = new object();
        Task _bgReadTask = null;
        CancellationTokenSource _bgReadTaskCancel = null;

        public DeIceProtocolMain(IDossySerial serial)
        {
            _serial = serial;
            startBgReadTask();

        }

        private void startBgReadTask()
        {
            lock (_bgReadLock)
            {
                if (_bgReadTask != null)
                    return;
                _bgReadTaskCancel = new CancellationTokenSource();
                var t = _bgReadTaskCancel.Token;
                _bgReadTask = Task.Run(() =>
                {

                    while (!_bgReadTaskCancel.IsCancellationRequested)
                    {
                        try
                        {
                            var firstbyte = _serial.PeekByte();
                            if (firstbyte < 0)
                            {
                                Thread.Sleep(1);
                            }
                            else if (firstbyte >= DeIceProtoConstants.FN_MIN || firstbyte == DeIceProtoConstants.FN_ERROR)
                            {
                                try
                                {
                                    var cmd = DoCommandReadInt();
                                    RaiseFunctionReceived(cmd);
                                }
                                catch (Exception ex)
                                {
                                    CommError?.Invoke(this, new DeIceComErrorEventArgs(ex));
                                }
                            }
                            else
                            {
                                firstbyte = _serial.ReadByte(SHORT_TIMEOUT); 
                                DoOobRead_int((byte)firstbyte);
                            }
                        } catch (Exception ex) { }
                    }
                }, t);
            }
        }

        private void stopBgReadTask()
        {
            lock (_bgReadLock)
            {
                if (_bgReadTask == null)
                    return;
                _bgReadTaskCancel?.Cancel();
                try
                {
                    _bgReadTask.Wait();
                } catch (Exception ex) {
                    Debug.WriteLine("HERERERER " + ex.ToString());
                } 
                _bgReadTask.Dispose();
                _bgReadTask = null;
                _bgReadTaskCancel?.Dispose();
                _bgReadTaskCancel = null;
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
            stopBgReadTask();
            try
            {
                byte[] buf = DeIceFnFactory.CreateDataFromReq(fn);
                _serial.Write(buf, 0, buf.Length, LONG_TIMEOUT);
                var r = DoCommandReadInt();
                if (r is DeIceFnReplyError)
                    throw new DeIceProtocolException($"Error returned from client, expected a {typeof(T).FullName}, received a {r?.GetType()?.Name ?? null}");
                if (r is not T)
                    throw new DeIceProtocolException($"Unexpected return from client, expected a {typeof(T).FullName}, received a {r?.GetType()?.Name ?? null}");
                return (T)r;
            }
            finally
            {
                startBgReadTask();
            }
        }

        public void SendReq(DeIceFnReqBase fn)
        {
            byte[] buf = DeIceFnFactory.CreateDataFromReq(fn);
            _serial.Write(buf, 0, buf.Length, SHORT_TIMEOUT);
            return;
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
        private bool disposedValue;

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

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < SHORT_TIMEOUT)
                {
                    int b = _serial.PeekByte();
                    if (b < 0)
                    {
                        Thread.Sleep(1);
                    }
                    else if (b >= DeIceProtoConstants.FN_MIN || b == DeIceProtoConstants.FN_ERROR)
                    {
                        sendStr();
                        return;
                    }
                    else
                    {
                        b = _serial.ReadByte(SHORT_TIMEOUT);
                        if (b == 0xD || b == 0xA)
                        {
                            sendStr();
                            return;
                        }
                        else
                        {
                            oobms.WriteByte((byte)b);
                            if (oobms.Length >= MAXOOB)
                            {
                                sendStr();
                                return;
                            }
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
                sendStr();
                return;
            }

        }

        private void RaiseOobData(string data)
        {
            foreach (var l in data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.TrimEntries))
                OobDataReceived?.Invoke(this, new DeIceOobDataReceivedEventArgs(l));
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
            FunctionReceived?.Invoke(this, new DeIceFunctionReceivedEventArgs(deIceFnBase));
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    stopBgReadTask();
                    if (_serial != null)
                    {
                        _serial.Dispose();
                        _serial = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DeIceProtocolMain()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
