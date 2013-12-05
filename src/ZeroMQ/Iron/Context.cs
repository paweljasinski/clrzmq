namespace ZeroMQ.Iron {
    using IronPython.Runtime;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ZeroMQ.Interop;

    [PythonType]
    public class Context {

        // This list holds external instance references to hashset containing weak references.
        // This gurantees, that when finalizer is called for a given instance, the weak references 
        // hold by this instance are not collected before. In particular, long weak references
        // have a valid references which can be used to dispose sockets.
        // The following does not have desired effect on the finalization during process termination.
        private static readonly List<HashSet<WeakReference>> ExternalRef = new List<HashSet<WeakReference>>();

        // holds weak refs to all active sockets
        private readonly HashSet<WeakReference> _sockets;

        // ReSharper disable InconsistentNaming

        internal IntPtr _zmq_ctx;

        public int? linger { get; set; }

        public Context(int io_threads = 1) {
            _sockets = new HashSet<WeakReference>();
            ExternalRef.Add(_sockets);
            if (io_threads < 0) {
                throw ZmqPyPkg.CreateZMQError(ErrorCode.EINVAL);
            }
            _zmq_ctx = LibZmq.zmq_ctx_new();
            if (_zmq_ctx == IntPtr.Zero) {
                throw ZmqPyPkg.CreateZMQError();
            }
            closed = false;
            LibZmq.zmq_ctx_set(_zmq_ctx, (int)ContextOption.IO_THREADS, io_threads);
        }

        ~Context() {
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " context finalizer");
            destroy();
            ExternalRef.Remove(_sockets);
        }

        public bool closed { get; private set; }

        // ReSharper disable ParameterHidesMember
        public void destroy(object linger = null) {
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " context destroy");
            // ReSharper restore ParameterHidesMember
            if (closed) {
                return;
            }
            // Console.WriteLine(_sockets == null);
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " sockets count:" + _sockets.Count);
            foreach (var wr in _sockets.ToList()) {
                // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " processing socket");
                var target = wr.Target;
                if (target == null) {
                    // alredy collected
                    continue;
                }
                var socket = (Socket)target;
                if (!socket.closed) {
                    if (linger != null) {
                        socket.set((int)SocketOption.LINGER, linger);
                    }
                    // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " about to call socket close from  destroy");
                    socket.close();
                }
            }
            DestroyIgnoreSignals();
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " context destroy completed");
        }

        public int get(int opt) {
            return LibZmq.zmq_ctx_get(_zmq_ctx, opt);
        }

        public int set(int opt, int optval) {
            return LibZmq.zmq_ctx_set(_zmq_ctx, opt, optval);
        }

        // ReSharper disable ParameterHidesMember
        public void term(object linger = null) {
            // ReSharper restore ParameterHidesMember
            if (closed) {
                return;
            }
            foreach (var wr in _sockets) {
                var target = wr.Target;
                if (target == null) {
                    continue;
                }
                var socket = (Socket)target;
                if (!socket.closed) {
                    if (linger != null) {
                        socket.set((int)SocketOption.LINGER, linger);
                    }
                }
            }
            // if there is any non closed socket, destroy context will block
            DestroyIgnoreSignals();
        }

        [PythonHidden]
        internal void _add_socket(Socket socket) {
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " _add_socket " + socket);
            _sockets.Add(new WeakReference(socket, true));
            if (linger != null) {
                socket.set((int)SocketOption.LINGER, linger);
            }
        }

        [PythonHidden]
        internal void _rm_socket(Socket socket) {
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " _rm_socket");
            var deadRow = new List<WeakReference>();
            foreach (var wr in _sockets) {
                var target = (Socket)wr.Target;
                if (target != null) {
                    if (target == socket) {
                        deadRow.Add(wr);
                    }
                } else {
                    // remove collected entries
                    deadRow.Add(wr);
                }
            }
            foreach (var wr in deadRow) {
                _sockets.Remove(wr);
            }
        }

        [PythonHidden]
        private void DestroyIgnoreSignals() {
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " destroy is not really called");
            // Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " destroy is called");
            while (LibZmq.zmq_ctx_destroy(_zmq_ctx) != 0) {
                if (LibZmq.zmq_errno() != ErrorCode.EINTR) {
                    break;
                }
            }
            _zmq_ctx = IntPtr.Zero;
            closed = true;
            GC.SuppressFinalize(this);
        }
    }
}
