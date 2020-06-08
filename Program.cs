using System;

namespace DotNetCoreProject
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = "/home/santosh";
            var scanner = new DirectoryScanner(path);
            scanner.Scan(useParallel: true);
            scanner.Scan(useParallel: false);

            Console.WriteLine("Press any key..");
            Console.ReadKey();
        }
    }
}
