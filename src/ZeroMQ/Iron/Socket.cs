namespace ZeroMQ.Iron {
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    using IronPython.Runtime;
    using IronPython.Runtime.Exceptions;
    using IronPython.Runtime.Operations;

    using Microsoft.Scripting;

    using ZeroMQ.Interop;

    [PythonType]
    public class Socket {

        // ReSharper disable InconsistentNaming
        public bool closed { get; private set; }

        public Context context { get; set; }

        public int socket_type { get; set; }

        public readonly List<string> debugAddr = new List<string>();

        public int fd {
            get {
                return (int)get(DefaultContext.Default, (int)SocketOption.FD);
            }
        }

        // I did not find a trivial way to follow zmq specification and test cases for
        // sockect attributed. 
        // The base zmq.Socket class must limit attribute list, where 
        // Subclass of the zmq.Socket does not restrict attribute creation
        // The following is a left over from the attempt to limit attribute list

        //
        //public void __setattr__(CodeContext cc, string name, object value) {
        //    //Console.WriteLine("this: " + this);
        //    //Console.WriteLine("cc:" + cc);
        //    //PythonType pt = DynamicHelpers.GetPythonType(this);
        //    //Console.WriteLine("pt:" + pt);
        //    //Console.WriteLine("name:" +  InstanceOps.SimpleRepr(pt));
        //    if (name == "closed") {
        //        closed = (bool)value;
        //    } else if (name == "context") {
        //        context = (Context)value;
        //    } else if (name == "fd") {
        //        throw new AttributeErrorException("fd can not be set");
        //    } else if (name == "closed") {
        //        throw new AttributeErrorException("closed can not be set");
        //    } else if (name == "linger") {
        //        linger = (int)value;
        //    // hwm has to be patched back into property
        //    } else if (name ==  "hwm") {
        //        PythonOps.Invoke(DefaultContext.Default, this, "set_hwm", value);
        //    } else if (name == "sndhwm") {
        //        set((int)SocketOption.SNDHWM, value);
        //    } else if (name == "rcvhwm") {
        //        set((int)SocketOption.RCVHWM, value);
        //    } else if (name == "identity") {
        //        set((int)SocketOption.IDENTITY, value);
        //    } else {
        //        throw new AttributeErrorException(String.Format("attribute: {0} is not allowed", name));
        //    }
        //}

        private int _linger;

        public int linger {
            get {
                return _linger;
            }
            set {
                _linger = value;
                set((int)SocketOption.LINGER, value);
            }
        }

        public int sndhwm {
            set {
                set((int)SocketOption.SNDHWM, value);
            }
            get {
                return (int)get(DefaultContext.Default, (int)SocketOption.SNDHWM);
            }
        }

        public int rcvhwm {
            set {
                set((int)SocketOption.RCVHWM, value);
            }
            get {
                return (int)get(DefaultContext.Default, (int)SocketOption.RCVHWM);
            }
        }

        public int type {
            get {
                return socket_type;
            }
        }

        public int rcvmore {
            get {
                return (int)get(DefaultContext.Default, (int)SocketOption.RCVMORE);
            }
        }

        public Bytes identity {
            set {
                set((int)SocketOption.IDENTITY, value);
            }
        }

        internal readonly IntPtr _zmq_socket;

        private readonly ZmqMsgT _zmqMsgT = new ZmqMsgT();

        public Socket(Context context, int sock_type) {
            this.context = context;
            socket_type = sock_type;
            _zmq_socket = LibZmq.zmq_socket(context._zmq_ctx, sock_type);
            if (_zmq_socket == IntPtr.Zero) {
                throw ZmqPyPkg.CreateZMQError();
            }
            context._add_socket(this);
            closed = false;
        }

        ~Socket() {
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " socket finalizer " + this);
            close();
        }

        // ReSharper disable ParameterHidesMember
        public int close(object linger = null) {
            // ReSharper restore ParameterHidesMember
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " about to close socket " + this);
            if (closed || _zmq_socket == IntPtr.Zero) {
                return 0;
            }
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " close");
            var rc = LibZmq.zmq_close(_zmq_socket);
            closed = true;
            context._rm_socket(this);
            GC.SuppressFinalize(this);
            return rc;
        }

        private string ArgAsString(object arg) {
            if (arg is string) {
                return (string)arg;
            }
            if (arg is Bytes) {
                return ((Bytes)arg).decode(DefaultContext.Default, "utf-8", "strict");
            }
            throw new TypeErrorException("address can be either string or bytes");
        }


        public void bind(object address) {
            var rc = LibZmq.zmq_bind(_zmq_socket, ArgAsString(address));
            debugAddr.Add("bind: " + ArgAsString(address));
            ZmqPyPkg.check_rc(rc);
        }

        public void unbind(object address) {
            var rc = LibZmq.zmq_unbind(_zmq_socket, ArgAsString(address));
            debugAddr.Add("unbind: " + ArgAsString(address));
            ZmqPyPkg.check_rc(rc);
        }

        public void connect(object address) {
            var rc = LibZmq.zmq_connect(_zmq_socket, ArgAsString(address));
            debugAddr.Add("connect: " + ArgAsString(address));
            ZmqPyPkg.check_rc(rc);
        }

        public void set(int option, object value) {
            int rc;
            if (value == null) {
                rc = LibZmq.zmq_setsockopt(_zmq_socket, option, IntPtr.Zero, 0);
            } else if (value is int) {
                // Console.WriteLine("setting value to: " + value);
                if (!ZmqPyPkg.isOptionType("int_sockopts", option)) {
                    throw ZmqPyPkg.CreateZMQError("EINVAL");
                }
                using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(int)))) {
                    Marshal.WriteInt32(optionValue, (int)value);
                    rc = LibZmq.zmq_setsockopt(_zmq_socket, option, optionValue.Ptr, sizeof(int));
                    if (rc == 0 && option == (int)SocketOption.LINGER) {
                        _linger = (int)value;
                    }
                }
            } else if (value is long) {
                if (!ZmqPyPkg.isOptionType("int64_sockopts", option)) {
                    throw new TypeErrorException(String.Format("argument type mismatch, option {0} does not accept int64 value", option));
                }
                using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(long)))) {
                    Marshal.WriteInt64(optionValue, (long)value);
                    rc = LibZmq.zmq_setsockopt(_zmq_socket, option, optionValue.Ptr, sizeof(long));
                }
                // implemented initially, very likely not used
                //} else if (value is ulong) {
                //    using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(ulong)))) {
                //        Marshal.WriteInt64(optionValue, unchecked(Convert.ToInt64(value)));
                //        rc = LibZmq.zmq_setsockopt(_zmq_socket, option, optionValue.Ptr, sizeof(ulong));
                //    }
                //} else if (value is byte[]) {
                //    var baValue = (byte[])value;
                //    using (var optionValue = new DisposableIntPtr(baValue.Length)) {
                //        Marshal.Copy(baValue, 0, optionValue, baValue.Length);
                //        rc = LibZmq.zmq_setsockopt(_zmq_socket, option, optionValue.Ptr, baValue.Length);
                //    }
            } else if (value is Bytes) {
                if (!ZmqPyPkg.isOptionType("bytes_sockopts", option)) {
                    throw new TypeErrorException(
                        String.Format("argument type mismatch, option {0} does not accept bytes value", option));
                }
                var baValue = ((Bytes)value).GetUnsafeByteArray();
                using (var optionValue = new DisposableIntPtr(baValue.Length)) {
                    Marshal.Copy(baValue, 0, optionValue, baValue.Length);
                    rc = LibZmq.zmq_setsockopt(_zmq_socket, option, optionValue.Ptr, baValue.Length);
                }
            } else {
                throw new TypeErrorException(
                    String.Format("value type can be either int, long or bytes; actual: {0}", value.GetType()));
            }
            ZmqPyPkg.check_rc(rc);
        }

        [PythonHidden]
        const int MaxBinaryOptionSize = 256;

        public object get(CodeContext cc, int option) {
            if (ZmqPyPkg.isOptionType("int_sockopts", option)) {
                using (var optionLength = new DisposableIntPtr(IntPtr.Size))
                using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(int)))) {
                    Marshal.WriteInt32(optionLength, sizeof(int));
                    var rc = LibZmq.zmq_getsockopt(_zmq_socket, option, optionValue.Ptr, optionLength.Ptr);
                    ZmqPyPkg.check_rc(rc);
                    return Marshal.ReadInt32(optionValue);
                }
            }
            if (ZmqPyPkg.isOptionType("int64_sockopts", option)) {
                using (var optionLength = new DisposableIntPtr(IntPtr.Size))
                using (var optionValue = new DisposableIntPtr(Marshal.SizeOf(typeof(long)))) {
                    Marshal.WriteInt64(optionLength, sizeof(long));
                    var rc = LibZmq.zmq_getsockopt(_zmq_socket, option, optionValue.Ptr, optionLength.Ptr);
                    ZmqPyPkg.check_rc(rc);
                    return Marshal.ReadInt64(optionValue);
                }
            }

            if (ZmqPyPkg.isOptionType("bytes_sockopts", option)) {
                using (var optionLength = new DisposableIntPtr(IntPtr.Size))
                using (var optionValue = new DisposableIntPtr(MaxBinaryOptionSize)) {
                    Marshal.WriteInt32(optionLength, MaxBinaryOptionSize);
                    var rc = LibZmq.zmq_getsockopt(_zmq_socket, option, optionValue.Ptr, optionLength.Ptr);
                    ZmqPyPkg.check_rc(rc);
                    var value = new byte[Marshal.ReadInt32(optionLength)];
                    Marshal.Copy(optionValue, value, 0, value.Length);
                    if (option != (int)SocketOption.IDENTITY && value[value.Length - 1] == 0) {
                        Array.Resize(ref value, value.Length - 1);
                    }
                    return new Bytes(value);
                }
            }
            throw ZmqPyPkg.CreateZMQError("EINVAL");
        }

        public object send(CodeContext cc, object message, int flags = 0, bool copy = true, bool track = false) {
            if (message is string) {
                throw new TypeErrorException("message can't be unicode (string)");
            }
            var len = 0;
            byte[] ba;
            if (message is byte[]) {
                ba = message as byte[];
            } else if (message is Bytes) {
                var tmp = message as Bytes;
                ba = tmp.GetUnsafeByteArray();
            } else if (message is ByteArray) {
                var tmp = message as ByteArray;
                ba = new byte[tmp.Count];
                tmp.CopyTo(ba, len);
            } else if (message is Frame) {
                ba = ((Frame)message).bytes.GetUnsafeByteArray();
            } else {
                throw new ArgumentTypeException(
                    String.Format("message can be either byte[], bytes or bytearray, is: {0}", message.GetType()));
            }
            len = ba.GetLength(0);
            _zmqMsgT.Init(len);
            Marshal.Copy(ba, 0, _zmqMsgT.Data(), len);
            var rc = LibZmq.zmq_msg_send(_zmqMsgT, _zmq_socket, flags);
            _zmqMsgT.Close();
            ZmqPyPkg.check_rc(rc);
            if (track) {
                object obj;
                if (!PythonOps.ModuleTryGetMember(cc, ZmqPyPkg.Mod, "MessageTracker", out obj)) {
                    throw new Exception("zmq is missing MessageTracker class");
                }
                var tracker = PythonCalls.Call(cc, obj);
                return tracker;
            }
            return null;
        }

        public object recv(CodeContext cc, int flags = 0, bool copy = true, bool track = false) {
            _zmqMsgT.Init();
            var len = LibZmq.zmq_msg_recv(_zmqMsgT, _zmq_socket, flags);
            if (len < 0) {
                _zmqMsgT.Close();
                ZmqPyPkg.check_rc(len);
            }
            var buffer = new byte[len];
            Marshal.Copy(_zmqMsgT.Data(), buffer, 0, len);
            _zmqMsgT.Close();
            var bytes = new Bytes(buffer);
            var frame = new Frame(cc, bytes, track);
            var tmp = get(cc, (int)SocketOption.RCVMORE);
            frame.more = tmp != null && ((int)tmp) != 0;
            if (copy) {
                return frame.bytes;
            }
            return frame;
        }

        public void monitor(CodeContext cc, string addr, int events = -1) {
            if (events < 0) {
                var constants = (PythonModule)ZmqPyPkg.Mod.__getattribute__(cc, "constants");
                events = (int)constants.__getattribute__(cc, "EVENT_ALL");
            }
            var rc = LibZmq.zmq_socket_monitor(_zmq_socket, addr, events);
            ZmqPyPkg.check_rc(rc);
        }

        // ReSharper restore InconsistentNaming
    }
}
