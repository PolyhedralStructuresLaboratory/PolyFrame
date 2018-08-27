using System;
using Rhino;
using Rhino.Commands;

namespace PolyFrame.DisplayConduitDraw
{
    public class MyCommand1 : Command
    {
        static MyCommand1 _instance;
        public MyCommand1()
        {
            _instance = this;
        }

        ///<summary>The only instance of the MyCommand1 command.</summary>
        public static MyCommand1 Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "MyCommand1"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.
            return Result.Success;
        }
    }
}