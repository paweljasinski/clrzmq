namespace ZeroMQ.Iron {
    using System;
    using IronPython.Runtime;
    using ZeroMQ.Interop;

    [PythonType]
    public class Stopwatch {
        // ReSharper disable InconsistentNaming

        private IntPtr watch;

        public Stopwatch() {

            watch = IntPtr.Zero;
        }

        public void start() {
            if (watch == IntPtr.Zero) {
                watch = LibZmq.zmq_stopwatch_start();
            } else {
                throw ZmqPyPkg.CreateZMQError("Stopwatch is already runing.");
            }
        }

        public long stop() {
            if (watch == IntPtr.Zero) {
                throw ZmqPyPkg.CreateZMQError("Must start the Stopwatch before calling stop.");
            }
            var time = LibZmq.zmq_stopwatch_stop(watch);
            watch = IntPtr.Zero;
            return time;
        }

        public void sleep(int seconds) {
            LibZmq.zmq_sleep(seconds);
        }
        // ReSharper enable InconsistentNaming
    }
}
