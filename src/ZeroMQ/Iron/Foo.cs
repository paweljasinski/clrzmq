using System;
using IronPython.Runtime;


namespace ZeroMQ.Iron {
    using IronPython.Runtime.Operations;

    [PythonType]
    public abstract class V {

        public V() {
        }

        public abstract int vm();

        public int x() {
            return 5 + vm();

        }
    }


    [PythonType]
    public class Foo {

        public Foo(CodeContext cc) {
            Console.WriteLine("constructor superclass");
            // Console.WriteLine(cc);
            // var ret = Builtin.__import__(cc, "bar");
            var ret = Importer.Import(cc, "bar", null, -1);
            var barModule = ret as PythonModule;
            if (barModule != null) {
                // Console.WriteLine(barModule);
                var attr = barModule.__getattribute__(cc, "fn");
                Console.WriteLine(attr);
                var fn = attr as PythonFunction;
                if (fn != null) {
                    PythonCalls.Call(cc, fn);
                }

            }
            // Console.WriteLine(ret);
        }

        public void baz(CodeContext cc) {
            Console.WriteLine("about to call foo from baz with explicit code context");
            PythonOps.Invoke(cc, this, "foo");
        }


        //public void baz() {
        //    // here try to call foo()
        //    Console.WriteLine("about to call foo from baz");
        //    PythonOps.Invoke(DefaultContext.Default,this,"foo");
        //}

    }

    [PythonType]
    public class Bar {
        public void bar() {
            Console.WriteLine("bar");
        }
    }
}
