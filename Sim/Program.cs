using System;
using System.Diagnostics;

namespace Sim
{
    class Program
    {
        static void Main(string[] args)
        {
            int demoId = 0;
            int nodeCount = 1;

            if (args != null && args.Length > 0)
            {
                // First arg is demoId
                Int32.TryParse(args[0], out demoId);

                if (args.Length > 1)
                {
                    // 2nd arg is count of nodes to launch
                    Int32.TryParse(args[1], out nodeCount);
                }
            }

            for(int i = 0; i < nodeCount; i++)
            {
                LaunchNode(demoId, nodeCount, i);
            }
        }

        static void LaunchNode(int demoId, int nodeCount, int NodeID)
        {
            // Launch Internet Explorer program passing the generated HTML file as a commandline arguement
            ProcessStartInfo startInfo = null;
            bool iserror = false;
            switch (demoId)
            {
                case 0: 
                    startInfo = new ProcessStartInfo(@"C:\UnoSys5G\UnoSys5G.Windows\bin\Debug\netcoreapp3.1\UnoSys5G.Windows.exe", $"{NodeID.ToString()} 0 ");
                    startInfo.WorkingDirectory = @"C:\UnoSys5G\UnoSys5G.Windows\bin\Debug\netcoreapp3.1\";
                    break;

                case 1:
                    startInfo = new ProcessStartInfo(@"C:\UnoSys5G\UnoSys5G.Windows\bin\Debug\netcoreapp3.1\UnoSys5G.Windows.exe", $"{NodeID.ToString()} 1");
                    startInfo.WorkingDirectory = @"C:\UnoSys5G\UnoSys5G.Windows\bin\Debug\netcoreapp3.1\";
                    break;

                case 2:
                    startInfo = new ProcessStartInfo(@"C:\UnoSys5G\UnoSys5G.Windows\bin\Debug\netcoreapp3.1\UnoSys5G.Windows.exe", $"{NodeID.ToString()} 2 " + (NodeID+1).ToString());
                    startInfo.WorkingDirectory = @"C:\UnoSys5G\UnoSys5G.Windows\bin\Debug\netcoreapp3.1\";
                    break;

                default:
                    Console.WriteLine($"ERROR:  Unknown demoid {demoId}");
                    iserror = true;
                    break;

            }
            if (! iserror)
            {
                Process.Start(startInfo);
            }
        }
    }
}
