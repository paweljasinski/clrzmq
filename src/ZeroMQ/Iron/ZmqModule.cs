using IronPython.Runtime;

[assembly: PythonModule("_zmq", typeof(ZeroMQ.Iron.ZmqModule))]

namespace ZeroMQ.Iron {
    using IronPython.Runtime.Operations;
    using System;
    using System.Runtime.InteropServices;
    using ZeroMQ.Interop;

    public static class ZmqModule {
        // ReSharper disable InconsistentNaming
        public const string __doc__ = @"description of the module";

        public static string strerror(int errno) {
            return Marshal.PtrToStringAnsi(LibZmq.zmq_strerror(errno));
        }

        public static int zmq_errno() {
            return LibZmq.zmq_errno();
        }

        public static PythonTuple zmq_version_info() {
            return new PythonTuple(new object[] { LibZmq.MajorVersion, LibZmq.MinorVersion, LibZmq.PatchVersion });
        }

        public static object zmq_poll(CodeContext cc, object sockets, long timeout) {
            var items = PythonOps.GetEnumerator(sockets);
            var pollItems = new PollItem[PythonOps.Length(sockets)];
            var i = 0;
            while (items.MoveNext()) {
                var pair = items.Current;
                var first = PythonOps.GetIndex(cc, pair, 0);
                var second = PythonOps.GetIndex(cc, pair, 1);
                pollItems[i].Events = (short)(int)second;
                pollItems[i].ReadyEvents = 0;
                if (first is int) {
                    // very likely ip has to convert internal managed fd and provide internal windows handle here
                    pollItems[i].FileDescriptor = new IntPtr((int)first);
                    pollItems[i].Socket = IntPtr.Zero;
                } else if (first is long) {
                    pollItems[i].FileDescriptor = new IntPtr(Convert.ToInt32(first));
                    pollItems[i].Socket = IntPtr.Zero;
                } else if (first is Socket) {
                    pollItems[i].FileDescriptor = IntPtr.Zero;
                    pollItems[i].Socket = ((Socket)first)._zmq_socket;
                } else if (PythonOps.HasAttr(cc, first, "fileno")) {
                    // in case of sockets, fileno returns windows handle, which is ok
                    // if it is a file from IronPython build-in, it is a made up file descriptor and is 
                    // never going to produce any reasonable result with zmq
                    // TODO: be clever here and perhaps for Python Files try to obtain SafeFileHandle
                    pollItems[i].FileDescriptor = new IntPtr((int)PythonOps.Invoke(cc, first, "fileno"));
                    pollItems[i].Socket = IntPtr.Zero;
                } else {
                    throw new ArgumentException("sockets contains invalid content, neither socket, fd or file like object detected at first possition");
                }
                i++;
            }

            LibZmq.zmq_poll(pollItems, pollItems.Length, timeout);
            var ret = new List();
            i = 0;
            foreach (var item in pollItems) {
                if (item.ReadyEvents > 0) {
                    object first;
                    if (item.Socket != IntPtr.Zero) {
                        // to match cffi and cython implementation, the original socket from argument is taken 
                        // but why?
                        var s1 = PythonOps.GetIndex(cc, sockets, i);
                        var val = PythonOps.GetIndex(cc, s1, 0);
                        first = val;
                    } else {
                        first = item.FileDescriptor.ToInt32();
                    }
                    ret.Add(new PythonTuple(new object[] { first, item.ReadyEvents }));
                }
                i++;
            }
            return ret;
        }

        public static void device(int device_type, Socket frontend, Socket backend) {
            proxy(frontend, backend);
        }

        public static void proxy(Socket frontend, Socket backend, Socket capture = null) {
            var capArg = IntPtr.Zero;
            if (capture != null) {
                capArg = capture._zmq_socket;
            }
            var rc = LibZmq.zmq_proxy(frontend._zmq_socket, backend._zmq_socket, capArg);
            ZmqPyPkg.check_rc(rc);
        }

        // ReSharper enable InconsistentNaming
    }
}
