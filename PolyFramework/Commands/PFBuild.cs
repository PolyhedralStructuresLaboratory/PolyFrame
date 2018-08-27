using System;
using Rhino;
using Rhino.Commands;

namespace PolyFrame.Utilities
{
    public class PFBuild : Command
    {
        static PFBuild _instance;
        public PFBuild()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PFBuild command.</summary>
        public static PFBuild Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PFBuild"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.
            return Result.Success;
        }
    }
}