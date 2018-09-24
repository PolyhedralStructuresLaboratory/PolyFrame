using Microsoft.VisualStudio.TestTools.UnitTesting;
using PolyFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyFramework.Utilities.Tests
{
    [TestClass()]
    public class MarchingTests
    {
       public TestContext TestContext { get; set; }


        [TestMethod()]
        public void InterpolateValueTest()
        {
            var marching = new MarchingCubes(0.5);
            var v0 = -1.0;
            var v1 = 1.0;


            Debug.WriteLine(marching.InterpolateValue(v0, v1).ToString());

            Assert.AreEqual(1,1);
        }
    }
}