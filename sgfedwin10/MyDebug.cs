using System;
using System.Collections.Generic;
using System.Diagnostics; // Conditional
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SgfEdwin10 {

    static class MyDbg {

        //// This Assert exists because Debug.Assert is broken in win8.  It does not put up a blocking dialog.
        //// It just logs the invariant failure in the VS Output Window but goes on to execute code that may
        //// not break immediately due to programmer error, but instead, just leave the model without integrity.
        ////
        [Conditional("DEBUG"), DebuggerNonUserCode()]
        public static void Assert (bool condition, string msg = "") {
            if (! condition) {
                var errmsg = "Debug Assert ...\n" + msg + "\n";
                // Cannot append stack trace in winrt.
                Debug.WriteLine(errmsg);
                //GameAux.Message(errmsg);
                Debugger.Break();
            }
        }

    }
}
