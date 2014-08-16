using System;
using System.Collections.Generic;
using System.Text;

namespace WordsToolkit.Common
{
    public class Trace
    {
        public static string Log(string message)
        {
            return message += message + "\r\n";
        }

        public static void LogStatus(MainPage rootPage, string message, NotifyType type)
        {
            rootPage.NotifyUser(message, type);
            Log(message);
        }
        

    }
}
