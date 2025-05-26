using System;

namespace Emby.Plugin.Danmu.Core
{
    public class DanmuDownloadErrorException : Exception
    {
        public DanmuDownloadErrorException(string message) : base(message)
        {
        }

        /// <summary>
        /// Don't display call stack as it's irrelevant
        /// </summary>
        public override string StackTrace
        {
            get { return ""; }
        }
    }
}