using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyFramework
{
    class EscapeKeyEventHandler : IDisposable
    {
        private bool _escapeKeyPressed = false;

        public EscapeKeyEventHandler(string message)
        {
            Rhino.RhinoApp.EscapeKeyPressed += new EventHandler(RhinoApp_EscapeKeyPressed);
            Rhino.RhinoApp.WriteLine(message);
        }

        public bool EscapeKeyPressed
        {
            get
            {
                Rhino.RhinoApp.Wait(); // "pumps" the Rhino message queue
                return _escapeKeyPressed;
            }
        }

        private void RhinoApp_EscapeKeyPressed(object sender, EventArgs e)
        {
            _escapeKeyPressed = true;
        }

        public void Dispose()
        {
            Rhino.RhinoApp.EscapeKeyPressed -= RhinoApp_EscapeKeyPressed;
        }
    }
}
