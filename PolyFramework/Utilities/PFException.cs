using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyFramework
{
    public class PolyFrameworkException : Exception
    {
        public PolyFrameworkException()
        {
        }

        public PolyFrameworkException(string message)
    : base(message)
        {
        }

        public PolyFrameworkException(string message, Exception inner)
    : base(message, inner)
        {
        }
    }

}
