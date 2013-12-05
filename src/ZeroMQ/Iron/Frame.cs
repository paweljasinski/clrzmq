using System;
using IronPython.Runtime;

namespace ZeroMQ.Iron {
    using System.Text;

    using IronPython.Runtime.Exceptions;
    using IronPython.Runtime.Operations;

    using Microsoft.Scripting;

    [PythonType]
    public class Frame {

        // ReSharper disable InconsistentNaming
        public bool more { get; set; }

        public object tracker { get; private set; } 

        public bool closed { get; private set; }

        public MemoryView buffer { get; private set; }

        public Frame(CodeContext cc, object data, bool track=false) {
            if (data is string) {
                throw new TypeErrorException("date can't be unicode (string)");
            }
            if (data is IBufferProtocol) {
                buffer = new MemoryView((IBufferProtocol)data);
            } else if (data is MemoryView ) {
                buffer = (MemoryView)data;
            } else {
                throw new ArgumentTypeException("data must be bytes or alike");
            }

            more = false;
            closed = false;
            tracker = null;
            if (track) {
                object obj;
                if (! PythonOps.ModuleTryGetMember(cc, ZmqPyPkg.Mod, "MessageTracker", out obj)) {
                    throw new Exception("zmq is missing MessageTracker class");
                }
                tracker = PythonCalls.Call(cc, obj);
            }
            
        }

        private Frame() {
        }

        private Frame fastCopy() {
            var newFrame = new Frame();
            newFrame.buffer = buffer;
            newFrame.tracker = tracker;
            return newFrame;
        }

        public Frame __copy__() {
            return fastCopy();
        }

        public Bytes bytes {
            get {
                return buffer.tobytes();
            }
            private set {
                throw new NotImplementedException();
            }
        }

        public int set(int option, object value) {
            throw new NotImplementedException();
        }

        public int get(int option) {
            throw new NotImplementedException();
        }

        public int __len__() {
            return buffer.__len__();
        }

        public bool __eq__(Frame other) {
            return this.bytes.Equals(other.bytes);
        }

        public string __str__() {
            return Encoding.UTF8.GetString(bytes.GetUnsafeByteArray(), 0, __len__());
        }
    }
}