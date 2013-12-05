namespace ZeroMQ.Iron {
    using IronPython.Runtime;
    using IronPython.Runtime.Exceptions;
    using IronPython.Runtime.Operations;
    using System;

    public sealed class ZmqPyPkg  {
        // ReSharper disable InconsistentNaming
        private static ZmqPyPkg instance = null;

        private readonly PythonModule _mod;

        private static readonly object padlock = new object();


        private ZmqPyPkg() {
            var tmp = Importer.Import(DefaultContext.Default, "zmq", null, -1);
            _mod = tmp as PythonModule;
        }

        public static PythonModule Mod {
            get {
                lock (padlock) {
                    if (instance != null) {
                        return instance._mod;
                    } 
                    instance = new ZmqPyPkg();
                    return instance._mod;
                }
            }
        }

        private static readonly CodeContext cc = DefaultContext.Default;

        public static Exception CreateZMQError() {
            return _createZMQError(ZmqModule.zmq_errno());
        }

        public static Exception CreateZMQError(string errnoName) {
            try {
                var err = _createZMQError((int)Mod.__getattribute__(cc, errnoName));
                return err;
            } catch (MissingMemberException) {
                return _createZMQError(errnoName);
            }
        }

        public static Exception CreateZMQError(int errno) {
            return _createZMQError(errno);
        }

        private static object _zmqError;

        private static Exception _createZMQError(object errno) { 
            if (_zmqError == null) { 
                if (! PythonOps.ModuleTryGetMember(cc, Mod, "ZMQError", out _zmqError)) {
                    throw new Exception("Unable to get reference to zmq.ZMQError");
                }
            }
            var ret = PythonCalls.Call(cc, _zmqError, errno);
            if (ret == null) {
                throw new Exception("unable to create exception");
            }
            var be = (PythonExceptions.BaseException)ret;
            return be.clsException;
        }

        private static object _check_rc;

        internal static void check_rc(int rc, int? errno = null) {
            if (_check_rc == null) {
                var mod = PythonOps.ImportWithNames(DefaultContext.Default, "zmq.error", new [] {"_check_rc"}, -1);
                _check_rc = PythonOps.ImportFrom(cc, mod, "_check_rc");
            }
            PythonCalls.Call(cc, _check_rc, new object[] { rc, errno });
        }

        public static bool isOptionType(string optionType, int option) {
            object opt;
            if (! PythonOps.ModuleTryGetMember(cc, constants, optionType, out opt)) {
                throw new Exception(String.Format("zmq.constants is missing definition for: {0}", optionType));
            }
            var sc = opt as SetCollection;
            if (sc == null) {
                throw new Exception(String.Format("unexpected type for zmq.constants.{0}", optionType));
            }
            return sc.__contains__(option);
        }

        public static bool isValidSocketOption(int option) {
            return isOptionType("bytes_sockopts", option) ||
                   isOptionType("int64_sockopts", option) ||
                   isOptionType("int_sockopts", option);
        }

        private static PythonModule _constants;

        public static PythonModule constants {
            get {
                if (_constants == null) {
                    object obj;
                    if (! PythonOps.ModuleTryGetMember(DefaultContext.Default, Mod, "constants", out obj)) {
                        throw new Exception("Unable to get reference to zmq.constants");
                    }
                    _constants = (PythonModule)obj;
                }
                return _constants;
            }
        }
    }
}
